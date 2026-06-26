using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>WebhookFunction.Run() のセキュリティパス・機能・境界値テスト。</summary>
public class WebhookFunctionTests
{
    private const string TestSecret     = "test-webhook-secret";
    private const string TestDiscordUrl = "https://discord.com/api/webhooks/123456/test-token";

    // ---- ヘルパーメソッド ----

    /// <summary>HMAC-SHA256 署名を計算する。</summary>
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    /// <summary>テスト用 HttpRequest を組み立てる。</summary>
    private static HttpRequest BuildRequest(
        string body,
        string secret,
        string eventName,
        string? webhookUrlParam = null,
        string? disabledEvents  = null,
        bool    omitSignature   = false,
        bool    omitEvent       = false,
        long?   contentLengthOverride = null)
    {
        var ctx       = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        ctx.Request.Method        = "POST";
        ctx.Request.Body          = new MemoryStream(bodyBytes);
        ctx.Request.ContentLength = contentLengthOverride ?? bodyBytes.Length;

        if (!omitSignature)
            ctx.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(bodyBytes, secret);

        if (!omitEvent)
            ctx.Request.Headers["X-GitHub-Event"] = eventName;

        var queryParams = new Dictionary<string, string?>();
        if (webhookUrlParam != null)
            queryParams["url"] = webhookUrlParam;
        if (disabledEvents != null)
            queryParams["disabled-events"] = disabledEvents;

        if (queryParams.Count > 0)
            ctx.Request.QueryString = QueryString.Create(queryParams);

        return ctx.Request;
    }

    /// <summary>WebhookFunction インスタンスを生成するファクトリ。</summary>
    private static WebhookFunction CreateFunction(
        Mock<IActionFactory>? factoryMock          = null,
        Mock<IMuteManager>?   muteMock             = null,
        string?               disabledEventsConfig = null,
        string?               discordUrlConfig     = null)
    {
        var factory = factoryMock ?? new Mock<IActionFactory>();

        // muteMock が指定されていない場合のみデフォルトを生成・設定する
        Mock<IMuteManager> mute;
        if (muteMock is null)
        {
            mute = new Mock<IMuteManager>();
            mute.Setup(m => m.EnsureLoadedAsync()).Returns(Task.CompletedTask);
            mute.Setup(m => m.IsMuted(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(false);
        }
        else
        {
            mute = muteMock;
        }

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["GITHUB_WEBHOOK_SECRET"]).Returns(TestSecret);
        configMock.Setup(c => c["DISCORD_WEBHOOK_URL"]).Returns(discordUrlConfig ?? TestDiscordUrl);
        if (disabledEventsConfig != null)
            configMock.Setup(c => c["DISABLED_EVENTS"]).Returns(disabledEventsConfig);

        var logger = new Mock<ILogger<WebhookFunction>>();

        return new WebhookFunction(factory.Object, mute.Object, configMock.Object, logger.Object);
    }

    // ---- セキュリティパステスト ----

    /// <summary>Content-Length が 10 MB 超のリクエストは 400 を返す。</summary>
    [Fact]
    public async Task Run_BodyTooLarge_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("{}", TestSecret, "push", contentLengthOverride: 11L * 1024 * 1024);

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>空ボディのリクエストは 400 を返す。</summary>
    [Fact]
    public async Task Run_EmptyBody_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("", TestSecret, "push");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>X-Hub-Signature-256 ヘッダーが欠落していると 400 を返す。</summary>
    [Fact]
    public async Task Run_MissingSignature_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("{}", TestSecret, "push", omitSignature: true);

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>無効な署名は 400 を返す。</summary>
    [Fact]
    public async Task Run_InvalidSignature_Returns400()
    {
        var fn  = CreateFunction();
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("{}");
        ctx.Request.Method        = "POST";
        ctx.Request.Body          = new MemoryStream(body);
        ctx.Request.ContentLength = body.Length;
        ctx.Request.Headers["X-Hub-Signature-256"] = "sha256=deadbeef";
        ctx.Request.Headers["X-GitHub-Event"]      = "push";

        var result = await fn.Run(ctx.Request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>X-GitHub-Event ヘッダーが欠落していると 400 を返す。</summary>
    [Fact]
    public async Task Run_MissingEventHeader_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("{}", TestSecret, "push", omitEvent: true);

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>X-GitHub-Event に特殊文字が含まれると 400 を返す（ログインジェクション防止）。</summary>
    [Fact]
    public async Task Run_EventHeaderWithSpecialChars_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("{}", TestSecret, "pull_request; DROP TABLE");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>?url= に非 Discord URL が指定されると 400 を返す（SSRF 対策）。</summary>
    [Fact]
    public async Task Run_NonDiscordWebhookUrl_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://evil.com/webhook");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>?url= に HTTP（非 HTTPS）の Discord URL が指定されると 400 を返す。</summary>
    [Fact]
    public async Task Run_HttpDiscordUrl_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "http://discord.com/api/webhooks/123/token");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>?url= に discord.com の有効な URL が指定された場合は正常処理する。</summary>
    [Fact]
    public async Task Run_ValidDiscordComUrl_Succeeds()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Returns(actionMock.Object);

        var fn  = CreateFunction(factoryMock: factoryMock);
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discord.com/api/webhooks/111/aaa");

        var result = await fn.Run(req);

        Assert.IsType<OkResult>(result);
    }

    /// <summary>?url= に discordapp.com の有効な URL が指定された場合は正常処理する。</summary>
    [Fact]
    public async Task Run_ValidDiscordappComUrl_Succeeds()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Returns(actionMock.Object);

        var fn  = CreateFunction(factoryMock: factoryMock);
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discordapp.com/api/webhooks/222/bbb");

        var result = await fn.Run(req);

        Assert.IsType<OkResult>(result);
    }

    // ---- イベント無効化テスト ----

    /// <summary>?disabled-events= で指定されたイベントは 202 を返す。</summary>
    [Fact]
    public async Task Run_DisabledEventViaQuery_Returns202()
    {
        var fn  = CreateFunction();
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            disabledEvents: "push,issues");

        var result = await fn.Run(req) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
    }

    /// <summary>設定値 DISABLED_EVENTS で指定されたイベントは 202 を返す。</summary>
    [Fact]
    public async Task Run_DisabledEventViaConfig_Returns202()
    {
        var fn  = CreateFunction(disabledEventsConfig: "push,issues");
        var req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push");

        var result = await fn.Run(req) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
    }

    // ---- ミュートテスト ----

    /// <summary>ミュート対象ユーザーからのリクエストは 200 "Muted user" を返す。</summary>
    [Fact]
    public async Task Run_MutedUser_Returns200WithMutedMessage()
    {
        var muteMock = new Mock<IMuteManager>();
        muteMock.Setup(m => m.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        muteMock.Setup(m => m.IsMuted(12345L, "push", It.IsAny<string?>())).Returns(true);

        var fn  = CreateFunction(muteMock: muteMock);
        var req = BuildRequest(
            """{"sender":{"id":12345,"login":"testuser"}}""",
            TestSecret,
            "push");

        var result = await fn.Run(req) as OkObjectResult;

        Assert.NotNull(result);
        var value = JsonSerializer.Serialize(result.Value);
        Assert.Contains("Muted user", value);
    }

    /// <summary>sender.id が数値でない場合（文字列）でも 500 にならず正常に処理する（F1 修正確認）。</summary>
    [Fact]
    public async Task Run_SenderIdIsString_DoesNotThrow_F1BugFix()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Returns(actionMock.Object);

        var fn  = CreateFunction(factoryMock: factoryMock);
        var req = BuildRequest(
            """{"sender":{"id":"evil","login":"testuser"}}""",
            TestSecret,
            "push");

        // InvalidOperationException でなく正常に完了すること
        var result = await fn.Run(req);

        // ミュートチェックをスキップして正常にアクション実行へ進む
        Assert.IsType<OkResult>(result);
    }

    // ---- JSON / ディスパッチテスト ----

    /// <summary>不正な JSON ボディは 400 を返す。</summary>
    [Fact]
    public async Task Run_InvalidJson_Returns400()
    {
        var fn  = CreateFunction();
        var req = BuildRequest("{ invalid json }", TestSecret, "push");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>ファクトリが NotImplementedException をスローした場合は 400 を返す。</summary>
    [Fact]
    public async Task Run_UnknownEvent_FactoryThrows_Returns400()
    {
        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Throws<NotImplementedException>();

        var fn  = CreateFunction(factoryMock: factoryMock);
        var req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "unknown_event");

        var result = await fn.Run(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>スタブアクション（RunAsync が NotImplementedException をスロー）は 406 を返す。</summary>
    [Fact]
    public async Task Run_StubAction_Returns406()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Throws<NotImplementedException>();

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Returns(actionMock.Object);

        var fn  = CreateFunction(factoryMock: factoryMock);
        var req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "stub_event");

        var result = await fn.Run(req) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(406, result.StatusCode);
    }

    /// <summary>ping イベントは正常処理されて 200 を返す。</summary>
    [Fact]
    public async Task Run_PingEvent_Returns200()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction("ping", It.IsAny<JsonElement>(), It.IsAny<string>()))
                   .Returns(actionMock.Object);

        var fn = CreateFunction(factoryMock: factoryMock);

        const string pingBody = """
        {
            "hook":{"type":"Repository","id":1,"events":["push"],"active":true},
            "sender":{"id":1,"login":"user"},
            "repository":{"id":1,"full_name":"owner/repo","html_url":"https://github.com/owner/repo","name":"repo"}
        }
        """;
        var req = BuildRequest(pingBody, TestSecret, "ping");

        var result = await fn.Run(req);

        Assert.IsType<OkResult>(result);
        actionMock.Verify(a => a.RunAsync(), Times.Once);
    }
}
