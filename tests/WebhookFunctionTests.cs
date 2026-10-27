using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
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
        long? contentLengthOverride = null,
        string? deliveryId = null)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var context = new FakeFunctionContext();

        HttpHeadersCollection headers = [];
        headers.Add("Content-Length", (contentLengthOverride ?? bodyBytes.LongLength).ToString(CultureInfo.InvariantCulture));

        if (!omitSignature)
            headers.Add("X-Hub-Signature-256", ComputeSignature(bodyBytes, secret));

        if (!omitEvent)
            headers.Add("X-GitHub-Event", eventName);

        if (deliveryId != null)
            headers.Add("X-GitHub-Delivery", deliveryId);

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
        string? discordUrlConfig = null,
        Mock<IWebhookQueue>? queueMock = null,
        ILogger<WebhookFunction>? loggerOverride = null)
    {
        Mock<IActionFactory> factory;
        if (factoryMock is null)
        {
            factory = new Mock<IActionFactory>();
            factory.Setup(f => f.IsRegistered(It.IsAny<string>())).Returns(true);
        }
        else
        {
            factory = factoryMock;
        }

        Mock<IWebhookQueue> queue = queueMock ?? new Mock<IWebhookQueue>();

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

        ILogger<WebhookFunction> logger = loggerOverride ?? new Mock<ILogger<WebhookFunction>>().Object;

        return new WebhookFunction(factory.Object, mute.Object, queue.Object, configMock.Object, logger);
    }

    /// <summary>Returns the single message handed to the queue mock, failing if there was none or more than one.</summary>
    private static WebhookQueueMessage SingleEnqueued(Mock<IWebhookQueue> queue)
    {
        var enqueued = queue.Invocations
            .Where(i => i.Method.Name == nameof(IWebhookQueue.EnqueueAsync))
            .Select(i => Assert.IsType<WebhookQueueMessage>(i.Arguments[0]))
            .ToList();
        return Assert.Single(enqueued);
    }

    private static Mock<IWebhookQueue> NewQueue()
    {
        Mock<IWebhookQueue> queue = new();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return queue;
    }

    private static void VerifyNothingEnqueued(Mock<IWebhookQueue> queue)
        => queue.Verify(
            q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private sealed class ListLogger : ILogger<WebhookFunction>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(formatter(state, exception) + exception);
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
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest("{}", TestSecret, "push", contentLengthOverride: 11L * 1024 * 1024);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        VerifyNothingEnqueued(queue);
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
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        var body = Encoding.UTF8.GetBytes("{}");
        var context = new FakeFunctionContext();
        HttpHeadersCollection headers = [];
        headers.Add("Content-Length", body.LongLength.ToString(CultureInfo.InvariantCulture));
        headers.Add("X-Hub-Signature-256", "sha256=deadbeef");
        headers.Add("X-GitHub-Event", "push");
        var req = new FakeHttpRequestData(context, new MemoryStream(body), headers, []);

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        VerifyNothingEnqueued(queue);
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
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://evil.com/webhook");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        VerifyNothingEnqueued(queue);
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

    /// <summary>A valid discord.com ?url= is queued as the destination and the request is accepted with 202.</summary>
    [Fact]
    public async Task RunValidDiscordComUrlEnqueuesWithThatUrl()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        const string body = """{"sender":{"id":1,"login":"u"}}""";
        HttpRequestData req = BuildRequest(
            body,
            TestSecret,
            "push",
            webhookUrlParam: "https://discord.com/api/webhooks/111/aaa",
            deliveryId: "72d3162e-cc78-11e3-81ab-4c9367dc0958");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        WebhookQueueMessage message = SingleEnqueued(queue);
        Assert.Equal("72d3162e-cc78-11e3-81ab-4c9367dc0958", message.DeliveryId);
        Assert.Equal("push", message.EventName);
        Assert.Equal(body, message.RawJson);
        Assert.Null(message.PayloadBlob);
        Assert.Equal(new Uri("https://discord.com/api/webhooks/111/aaa"), message.WebhookUrl);
    }

    /// <summary>A valid discordapp.com ?url= is accepted and queued.</summary>
    [Fact]
    public async Task RunValidDiscordappComUrlEnqueuesWithThatUrl()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discordapp.com/api/webhooks/222/bbb");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Equal(new Uri("https://discordapp.com/api/webhooks/222/bbb"), SingleEnqueued(queue).WebhookUrl);
    }

    /// <summary>Without ?url= the default webhook is not stored in the queue (null), so its token never reaches storage.</summary>
    [Fact]
    public async Task RunWithoutUrlParamEnqueuesNullWebhookUrl()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Null(SingleEnqueued(queue).WebhookUrl);
    }

    /// <summary>A missing X-GitHub-Delivery header, or one outside the allowed alphabet, is replaced by a generated GUID.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("abc;injected")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task RunInvalidDeliveryIdIsReplacedWithGuid(string? deliveryId)
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "push", deliveryId: deliveryId);

        await fn.RunAsync(req);

        Assert.True(Guid.TryParse(SingleEnqueued(queue).DeliveryId, out _));
    }

    /// <summary>When the queue write fails, the request is not acknowledged (500) and the log does not contain the webhook token.</summary>
    [Fact]
    public async Task RunEnqueueFailureReturns500WithoutLoggingTheWebhookToken()
    {
        Mock<IWebhookQueue> queue = new();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        ListLogger logger = new();
        WebhookFunction fn = CreateFunction(queueMock: queue, loggerOverride: logger);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            webhookUrlParam: "https://discord.com/api/webhooks/111/SECRET-TOKEN");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Contains(logger.Entries, e => e.Contains("Failed to enqueue webhook event: push", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Contains("SECRET-TOKEN", StringComparison.Ordinal));
    }

    // ---- Event-disabling tests ----

    /// <summary>An event specified in ?disabled-events= returns 202.</summary>
    [Fact]
    public async Task RunDisabledEventViaQueryReturns202()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":1,"login":"u"}}""",
            TestSecret,
            "push",
            disabledEvents: "push,issues");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        VerifyNothingEnqueued(queue);
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

        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(muteMock: muteMock, queueMock: queue);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":12345,"login":"testuser"}}""",
            TestSecret,
            "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("Muted user", ReadBody(result), StringComparison.Ordinal);
        VerifyNothingEnqueued(queue);
    }

    /// <summary>Even when sender.id is not a number (a string), it does not return 500 and the event is queued.</summary>
    [Fact]
    public async Task RunSenderIdAsStringDoesNotThrow()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpRequestData req = BuildRequest(
            """{"sender":{"id":"evil","login":"testuser"}}""",
            TestSecret,
            "push");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        SingleEnqueued(queue);
    }

    /// <summary>When the sender field is missing, the mute check is skipped and the event is queued.</summary>
    [Fact]
    public async Task RunSenderFieldMissingSkipsMuteCheckAndContinues()
    {
        Mock<IWebhookQueue> queue = NewQueue();
        HttpRequestData req = BuildRequest(
            body: """{"action":"opened"}""",
            secret: TestSecret,
            eventName: "push");

        WebhookFunction fn = CreateFunction(queueMock: queue);
        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        SingleEnqueued(queue);
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

    /// <summary>An event with no registered handler returns 406 and nothing is queued.</summary>
    [Fact]
    public async Task RunUnregisteredEventReturns406AndEnqueuesNothing()
    {
        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.IsRegistered("unknown_event")).Returns(false);
        Mock<IWebhookQueue> queue = NewQueue();

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock, queueMock: queue);
        HttpRequestData req = BuildRequest("""{"sender":{"id":1,"login":"u"}}""", TestSecret, "unknown_event");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.NotAcceptable, result.StatusCode);
        Assert.Contains("Method not implemented", ReadBody(result), StringComparison.Ordinal);
        VerifyNothingEnqueued(queue);
    }

    /// <summary>A ping event is queued and acknowledged with 202; the action is not run on the request path.</summary>
    [Fact]
    public async Task RunPingEventEnqueuesAndReturns202()
    {
        var factoryMock = new Mock<IActionFactory>();
        factoryMock.Setup(f => f.IsRegistered("ping")).Returns(true);
        Mock<IWebhookQueue> queue = NewQueue();

        WebhookFunction fn = CreateFunction(factoryMock: factoryMock, queueMock: queue);

        const string pingBody = """
        {
            "hook":{"type":"Repository","id":1,"events":["push"],"active":true},
            "sender":{"id":1,"login":"user"},
            "repository":{"id":1,"full_name":"owner/repo","html_url":"https://github.com/owner/repo","name":"repo"}
        }
        """;
        HttpRequestData req = BuildRequest(pingBody, TestSecret, "ping");

        HttpResponseData result = await fn.RunAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Equal("ping", SingleEnqueued(queue).EventName);
        factoryMock.Verify(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()), Times.Never);
    }
}
