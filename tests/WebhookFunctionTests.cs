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

/// <summary>Security-path, functional, and boundary-value tests for WebhookFunction.RunAsync().</summary>
public class WebhookFunctionTests
{
    private const string TestSecret = "test-webhook-secret";
    private const string TestDiscordUrl = "https://discord.com/api/webhooks/123456/test-token";

    // ---- Helper methods ----

    /// <summary>Computes the HMAC-SHA256 signature.</summary>
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    /// <summary>Builds an <see cref="HttpRequestData"/> for tests.</summary>
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

    /// <summary>Factory that creates a WebhookFunction instance.</summary>
    private static WebhookFunction CreateFunction(
        Mock<IActionFactory>? factoryMock = null,
        Mock<IMuteManager>? muteMock = null,
        string? disabledEventsConfig = null,
        string? discordUrlConfig = null)
    {
        Mock<IActionFactory> factory = factoryMock ?? new Mock<IActionFactory>();

        // Create and configure a default only when muteMock is not provided
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

    /// <summary>Reads the response body as a UTF-8 string.</summary>
    private static string ReadBody(HttpResponseData response)
    {
        var stream = (MemoryStream)response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // ---- Security-path tests ----

    /// <summary>A request with Content-Length over 10 MB returns 400.</summary>
    [Fact]
    public async Task RunBodyTooLargeReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", contentLengthOverride: 11L * 1024 * 1024);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>A request with an empty body returns 400.</summary>
    [Fact]
    public async Task RunEmptyBodyReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("", TestSecret, "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>Returns 400 when the X-Hub-Signature-256 header is missing.</summary>
    [Fact]
    public async Task RunMissingSignatureReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", omitSignature: true);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>An invalid signature returns 400.</summary>
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

    /// <summary>Returns 400 when the X-GitHub-Event header is missing.</summary>
    [Fact]
    public async Task RunMissingEventHeaderReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", omitEvent: true);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>Returns 400 when X-GitHub-Event contains special characters (log injection prevention).</summary>
    [Fact]
    public async Task RunEventHeaderWithSpecialCharsReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{}", TestSecret, "pull_request; DROP TABLE");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>Returns 400 when a non-Discord URL is passed in ?url= (SSRF protection).</summary>
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

    /// <summary>Returns 400 when an HTTP (non-HTTPS) Discord URL is passed in ?url=.</summary>
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

    /// <summary>Processes normally when a valid discord.com URL is passed in ?url=.</summary>
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

    /// <summary>Processes normally when a valid discordapp.com URL is passed in ?url=.</summary>
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

    // ---- Event-disabling tests ----

    /// <summary>An event specified in ?disabled-events= returns 202.</summary>
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

    /// <summary>An event specified in the DISABLED_EVENTS config value returns 202.</summary>
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

    // ---- Mute tests ----

    /// <summary>A request from a muted user returns 200 "Muted user".</summary>
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

    /// <summary>Even when sender.id is not a number (a string), it does not return 500 and processes normally.</summary>
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

    /// <summary>When the sender field is missing, the mute check is skipped and processing continues normally.</summary>
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

    // ---- JSON / dispatch tests ----

    /// <summary>An invalid JSON body returns 400.</summary>
    [Fact]
    public async Task RunInvalidJsonReturns400()
    {
        WebhookFunction fn = CreateFunction();
        HttpRequestData req = BuildRequest("{ invalid json }", TestSecret, "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>Returns 400 when the factory throws a NotImplementedException.</summary>
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

    /// <summary>A stub action (whose RunAsync throws NotImplementedException) returns 406.</summary>
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

    /// <summary>A ping event is processed normally and returns 200.</summary>
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
