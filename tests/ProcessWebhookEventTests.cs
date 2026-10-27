using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="WebhookEventProcessor"/>: delivery, rate-limit rescheduling, dead-lettering, and retry propagation.</summary>
public class ProcessWebhookEventTests
{
    private const string RawJson = """{"sender":{"id":1}}""";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 3, 0, 0, TimeSpan.Zero);
    private static readonly Uri _defaultUrl = new("https://discord.com/api/webhooks/1/default-token");
    private static readonly Uri _queuedUrl = new("https://discord.com/api/webhooks/2/queued-token");

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ListLogger : ILogger<WebhookEventProcessor>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class Fixture
    {
        public Mock<IWebhookQueue> Queue { get; } = new(MockBehavior.Strict);
        public Mock<IActionFactory> Factory { get; } = new(MockBehavior.Strict);
        public Mock<IAction> Action { get; } = new(MockBehavior.Strict);
        public ListLogger Logger { get; } = new();
        public WebhookEventProcessor Processor { get; }

        public Fixture()
        {
            Mock<IConfiguration> config = new();
            config.Setup(c => c["DISCORD_WEBHOOK_URL"]).Returns(_defaultUrl.AbsoluteUri);
            Processor = new WebhookEventProcessor(Queue.Object, Factory.Object, config.Object, new FixedTimeProvider(Now), Logger);
        }

        public void SetupPayload(WebhookQueueMessage message, string raw = RawJson)
            => Queue.Setup(q => q.LoadPayloadAsync(message, It.IsAny<CancellationToken>())).ReturnsAsync(raw);

        public void SetupAction(Uri expectedUrl, Func<Task> run)
        {
            Factory.Setup(f => f.GetAction("push", RawJson, expectedUrl)).Returns(Action.Object);
            Action.Setup(a => a.RunAsync()).Returns(run);
        }

        public void SetupEnqueue()
            => Queue.Setup(q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        public void SetupDeadLetter()
            => Queue.Setup(q => q.DeadLetterAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private static WebhookQueueMessage Message(Uri? url = null, DateTimeOffset? receivedAt = null, string? payloadBlob = null)
        => new("delivery-1", "push", payloadBlob is null ? RawJson : null, payloadBlob, url, receivedAt ?? Now);

    [Fact]
    public async Task ProcessAsync_Success_RunsTheActionOnceWithTheConfiguredDefaultWebhook()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => Task.CompletedTask);

        await fx.Processor.ProcessAsync(message, 1);

        fx.Action.Verify(a => a.RunAsync(), Times.Once);
        fx.Queue.Verify(q => q.LoadPayloadAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(fx.Logger.Entries);
    }

    [Fact]
    public async Task ProcessAsync_Success_UsesTheQueuedWebhookUrl()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(_queuedUrl);
        fx.SetupPayload(message);
        fx.SetupAction(_queuedUrl, () => Task.CompletedTask);

        await fx.Processor.ProcessAsync(message, 1);

        fx.Factory.Verify(f => f.GetAction("push", RawJson, _queuedUrl), Times.Once);
    }

    [Theory]
    [InlineData(7, 7, 12)]
    [InlineData(0.1, 1, 6)]
    [InlineData(1800, 900, 905)]
    public async Task ProcessAsync_RateLimit_ReEnqueuesWithClampedDelayPlusJitter(double retryAfterSeconds, double minSeconds, double maxSecondsExclusive)
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(receivedAt: Now - TimeSpan.FromMinutes(5));
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new DiscordRateLimitException(TimeSpan.FromSeconds(retryAfterSeconds), isGlobal: false, scope: "user"));
        TimeSpan? delay = null;
        WebhookQueueMessage? reEnqueued = null;
        fx.Queue.Setup(q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookQueueMessage, TimeSpan?, CancellationToken>((m, d, _) => (reEnqueued, delay) = (m, d))
            .Returns(Task.CompletedTask);

        await fx.Processor.ProcessAsync(message, 1);

        Assert.Equal(message, reEnqueued);
        Assert.InRange(delay!.Value.TotalSeconds, minSeconds, maxSecondsExclusive - 1e-9);
        (LogLevel level, var text) = Assert.Single(fx.Logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.StartsWith("Discord rate limit: retrying in ", text, StringComparison.Ordinal);
        Assert.EndsWith("s (scope=user, global=False, DeliveryId=delivery-1)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_RateLimitedBlobBackedMessage_ReEnqueuesTheSameReference()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(payloadBlob: "20261027/abc.json");
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new DiscordRateLimitException(TimeSpan.FromSeconds(3), false, null));
        fx.SetupEnqueue();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.EnqueueAsync(message, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_RateLimitedForExactlySixHours_IsStillRescheduled()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(receivedAt: Now - WebhookEventProcessor.MaxAge);
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new DiscordRateLimitException(TimeSpan.FromSeconds(3), false, null));
        fx.SetupEnqueue();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.EnqueueAsync(message, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_RateLimitedForMoreThanSixHours_IsDeadLetteredNotRescheduled()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(receivedAt: Now - WebhookEventProcessor.MaxAge - TimeSpan.FromSeconds(1));
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new DiscordRateLimitException(TimeSpan.FromSeconds(3), false, null));
        fx.SetupDeadLetter();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.DeadLetterAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        (LogLevel level, var text) = Assert.Single(fx.Logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal("Webhook event dead-lettered: rate limited for more than 6 hours (DeliveryId=delivery-1, EventName=push)", text);
    }

    [Fact]
    public async Task ProcessAsync_ReEnqueueFails_PropagatesSoTheHostRetries()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new DiscordRateLimitException(TimeSpan.FromSeconds(3), false, null));
        fx.Queue.Setup(q => q.EnqueueAsync(It.IsAny<WebhookQueueMessage>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fx.Processor.ProcessAsync(message, 1));
    }

    [Fact]
    public async Task ProcessAsync_OtherFailureBeforeFinalAttempt_PropagatesWithoutLogging()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new HttpRequestException("Discord API error: 500 Internal Server Error"));

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => fx.Processor.ProcessAsync(message, WebhookEventProcessor.MaxDequeueCount - 1));

        Assert.Equal("Discord API error: 500 Internal Server Error", ex.Message);
        Assert.Empty(fx.Logger.Entries);
    }

    [Fact]
    public async Task ProcessAsync_OtherFailureOnFinalAttempt_LogsDeadLetteredAndRethrows()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new HttpRequestException("Discord API error: 500 Internal Server Error"));

        await Assert.ThrowsAsync<HttpRequestException>(() => fx.Processor.ProcessAsync(message, WebhookEventProcessor.MaxDequeueCount));

        (LogLevel level, var text) = Assert.Single(fx.Logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal("Webhook event dead-lettered: delivery failed 10 times (DeliveryId=delivery-1, EventName=push)", text);
    }

    [Fact]
    public async Task ProcessAsync_QueuedUrlOutsideTheAllowList_IsDeadLetteredWithoutCallingDiscord()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(new Uri("https://evil.example/api/webhooks/1/x"));
        fx.SetupDeadLetter();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.DeadLetterAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        fx.Factory.Verify(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()), Times.Never);
        Assert.Equal(
            "Webhook event dead-lettered: webhook URL is not an allowed Discord webhook URL (DeliveryId=delivery-1, EventName=push)",
            Assert.Single(fx.Logger.Entries).Text);
    }

    [Fact]
    public async Task ProcessAsync_MissingPayloadBlob_IsDeadLettered()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message(payloadBlob: "20261027/gone.json");
        fx.Queue.Setup(q => q.LoadPayloadAsync(message, It.IsAny<CancellationToken>())).ThrowsAsync(new PayloadMissingException("gone"));
        fx.SetupDeadLetter();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.DeadLetterAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(
            "Webhook event dead-lettered: payload is missing (DeliveryId=delivery-1, EventName=push)",
            Assert.Single(fx.Logger.Entries).Text);
    }

    [Theory]
    [InlineData(typeof(JsonException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task ProcessAsync_UndeserializablePayload_IsDeadLetteredImmediately(Type exceptionType)
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.Factory.Setup(f => f.GetAction("push", RawJson, _defaultUrl)).Throws((Exception)Activator.CreateInstance(exceptionType, "bad")!);
        fx.SetupDeadLetter();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.DeadLetterAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(
            "Webhook event dead-lettered: payload cannot be deserialized (DeliveryId=delivery-1, EventName=push)",
            Assert.Single(fx.Logger.Entries).Text);
    }

    [Fact]
    public async Task ProcessAsync_ActionWithoutHandler_IsDeadLettered()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = Message();
        fx.SetupPayload(message);
        fx.SetupAction(_defaultUrl, () => throw new NotImplementedException("no handler"));
        fx.SetupDeadLetter();

        await fx.Processor.ProcessAsync(message, 1);

        fx.Queue.Verify(q => q.DeadLetterAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }
}
