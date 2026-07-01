using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Managers;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>WebhookFunction.RunAsync() のセキュリティパス・機能・境界値テスト。</summary>
public class WebhookFunctionTests
{
    private const string TestSecret = "test-webhook-secret";
    private const string TestDiscordUrl = "https://discord.com/api/webhooks/123456/test-token";

    // ---- ヘルパーメソッド ----

    /// <summary>HMAC-SHA256 署名を計算する。</summary>
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    /// <summary>テスト用 <see cref="HttpRequestData"/> を組み立てる。</summary>
    private static FakeHttpRequestData BuildRequest(
        string body,
        string secret,
        string eventName,
        string? webhookUrlParam = null,
        string? disabledEvents = null,
        bool omitSignature = false,
        bool omitEvent = false,
        long? contentLengthOverride = null)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var context = new FakeFunctionContext();

        HttpHeadersCollection headers = [];
        headers.Add("Content-Length", (contentLengthOverride ?? bodyBytes.LongLength).ToString(CultureInfo.InvariantCulture));

        if (!omitSignature)
            headers.Add("X-Hub-Signature-256", ComputeSignature(bodyBytes, secret));

        if (!omitEvent)
            headers.Add("X-GitHub-Event", eventName);

        NameValueCollection query = [];
        if (webhookUrlParam != null)
            query["url"] = webhookUrlParam;
        if (disabledEvents != null)
            query["disabled-events"] = disabledEvents;

        return new FakeHttpRequestData(context, new MemoryStream(bodyBytes), headers, query);
    }

    /// <summary>WebhookFunction インスタンスを生成するファクトリ。</summary>
    private static WebhookFunction CreateFunction(
        Mock<IActionFactory>? factoryMock = null,
        Mock<IMuteManager>? muteMock = null,
        string? disabledEventsConfig = null,
        string? discordUrlConfig = null)
    {
        Mock<IActionFactory> factory = factoryMock ?? new Mock<IActionFactory>();

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

    /// <summary>レスポンスボディを UTF-8 文字列として読み出す。</summary>
    private static string ReadBody(HttpResponseData response)
    {
        var stream = (MemoryStream)response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // ---- セキュリティパステスト ----

    /// <summary>Content-Length が 10 MB 超のリクエストは 400 を返す。</summary>
    [Fact]
    public async Task RunBodyTooLargeReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", contentLengthOverride: 11L * 1024 * 1024);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>空ボディのリクエストは 400 を返す。</summary>
    [Fact]
    public async Task RunEmptyBodyReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("", TestSecret, "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>X-Hub-Signature-256 ヘッダーが欠落していると 400 を返す。</summary>
    [Fact]
    public async Task RunMissingSignatureReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", omitSignature: true);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>無効な署名は 400 を返す。</summary>
    [Fact]
    public async Task RunInvalidSignatureReturns400()
    {
        WebhookFunction fn = CreateFunction();
        var body = Encoding.UTF8.GetBytes("{}");
        var context = new FakeFunctionContext();
        HttpHeadersCollection headers = [];
        headers.Add("Content-Length", body.LongLength.ToString(CultureInfo.InvariantCulture));
        headers.Add("X-Hub-Signature-256", "sha256=deadbeef");
        headers.Add("X-GitHub-Event", "push");
        var req = new FakeHttpRequestData(context, new MemoryStream(body), headers, []);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>X-GitHub-Event ヘッダーが欠落していると 400 を返す。</summary>
    [Fact]
    public async Task RunMissingEventHeaderReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", omitEvent: true);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>X-GitHub-Event に特殊文字が含まれると 400 を返す（ログインジェクション防止）。</summary>
    [Fact]
    public async Task RunEventHeaderWithSpecialCharsReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "pull_request; DROP TABLE");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>?url= に非 Discord URL が指定されると 400 を返す（SSRF 対策）。</summary>
    [Fact]
    public async Task RunNonDiscordWebhookUrlReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://evil.com/webhook");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>?url= に HTTP（非 HTTPS）の Discord URL が指定されると 400 を返す。</summary>
    [Fact]
    public async Task RunHttpDiscordUrlReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "http://discord.com/api/webhooks/123/token");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>?url= に discord.com の有効な URL が指定された場合は正常処理する。</summary>
    [Fact]
    public async Task RunValidDiscordComUrlSucceeds()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
                   .Returns(actionMock.Object);

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discord.com/api/webhooks/111/aaa");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    /// <summary>?url= に discordapp.com の有効な URL が指定された場合は正常処理する。</summary>
    [Fact]
    public async Task RunValidDiscordappComUrlSucceeds()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
                   .Returns(actionMock.Object);

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discordapp.com/api/webhooks/222/bbb");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    // ---- イベント無効化テスト ----

    /// <summary>?disabled-events= で指定されたイベントは 202 を返す。</summary>
    [Fact]
    public async Task RunDisabledEventViaQueryReturns202()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            disabledEvents: "push,issues");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
    }

    /// <summary>設定値 DISABLED_EVENTS で指定されたイベントは 202 を返す。</summary>
    [Fact]
    public async Task RunDisabledEventViaConfigReturns202()
    {
        WebhookFunction fn = CreateFunction(disabledEventsConfig: "push,issues");
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
    }

    // ---- ミュートテスト ----

    /// <summary>ミュート対象ユーザーからのリクエストは 200 "Muted user" を返す。</summary>
    [Fact]
    public async Task RunMutedUserReturns200WithMutedMessage()
    {
        var muteMock = new Mock<IMuteManager>();
        muteMock.Setup(m => m.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        muteMock.Setup(m => m.IsMuted(12345L, "push", It.IsAny<string?>())).Returns(true);

        WebhookFunction fn = CreateFunction(muteMock: muteMock);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":12345,"login":"testuser"}}""",
            TestSecret,
            "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("Muted user", ReadBody(result), StringComparison.Ordinal);
    }

    /// <summary>sender.id が数値でない場合（文字列）でも 500 にならず正常に処理する。</summary>
    [Fact]
    public async Task RunSenderIdAsStringDoesNotThrow()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
                   .Returns(actionMock.Object);

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":"evil","login":"testuser"}}""",
            TestSecret,
            "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    /// <summary>sender フィールドが存在しない場合はミュートチェックをスキップして正常処理する。</summary>
    [Fact]
    public async Task RunSenderFieldMissingSkipsMuteCheckAndContinues()
    {
        Mock<IActionFactory> factory = new();
        Mock<IAction> action = new();
        action.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);
        factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
               .Returns(action.Object);

        HttpRequestData req = BuildRequest(
            body: """{"action":"opened"}""",
            secret: TestSecret,
            eventName: "push");

        WebhookFunction fn = CreateFunction(factoryMock: factory);
        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    // ---- JSON / ディスパッチテスト ----

    /// <summary>不正な JSON ボディは 400 を返す。</summary>
    [Fact]
    public async Task RunInvalidJsonReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{ invalid json }", TestSecret, "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>ファクトリが NotImplementedException をスローした場合は 400 を返す。</summary>
    [Fact]
    public async Task RunUnknownEventFactoryThrowsReturns400()
    {
        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
                   .Throws<NotImplementedException>();

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);
        HttpRequestData req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "unknown_event");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>スタブアクション（RunAsync が NotImplementedException をスロー）は 406 を返す。</summary>
    [Fact]
    public async Task RunStubActionReturns406()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Throws<NotImplementedException>();

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
                   .Returns(actionMock.Object);

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);
        HttpRequestData req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "stub_event");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.NotAcceptable, result.StatusCode);
    }

    /// <summary>ping イベントは正常処理されて 200 を返す。</summary>
    [Fact]
    public async Task RunPingEventReturns200()
    {
        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);

        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.GetAction("ping", It.IsAny<string>(), It.IsAny<Uri>()))
                   .Returns(actionMock.Object);

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock);

        const string pingBody = """
        {
            "hook":{"type":"Repository","id":1,"events":["push"],"active":true},
            "sender":{"id":1,"login":"user"},
            "repository":{"id":1,"full_name":"owner/repo","html_url":"https://github.com/owner/repo","name":"repo"}
        }
        """;
        HttpRequestData req = BuildRequest(pingBody, TestSecret, "ping");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        actionMock.Verify(a => a.RunAsync(), Times.Once);
    }
}
