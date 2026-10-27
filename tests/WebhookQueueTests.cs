using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using GitHubWebhookBridge.Services;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="WebhookQueue"/>: Base64 envelope, claim-check threshold, dead-lettering, and payload loading.</summary>
public class WebhookQueueTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 27, 1, 6, 23, TimeSpan.Zero);

    private sealed class Fixture
    {
        public Mock<QueueClient> Events { get; } = new(MockBehavior.Strict);
        public Mock<QueueClient> Poison { get; } = new(MockBehavior.Strict);
        public Mock<BlobContainerClient> Payloads { get; } = new(MockBehavior.Strict);
        public List<(string Body, TimeSpan? Delay)> EventSends { get; } = [];
        public List<string> PoisonSends { get; } = [];

        public WebhookQueue Queue => new(Events.Object, Poison.Object, Payloads.Object);

        public Fixture()
        {
            Events.Setup(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((body, delay, _, _) => EventSends.Add((body, delay)))
                .ReturnsAsync(Receipt());
            Poison.Setup(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((body, _, _, _) => PoisonSends.Add(body))
                .ReturnsAsync(Receipt());
        }

        private static Response<SendReceipt> Receipt()
            => Response.FromValue(
                QueuesModelFactory.SendReceipt("id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "pop", DateTimeOffset.UtcNow),
                Mock.Of<Response>());
    }

    private static WebhookQueueMessage Inline(string rawJson, string? webhookUrl = null)
        => new("delivery-1", "push", rawJson, null, webhookUrl is null ? null : new Uri(webhookUrl), ReceivedAt);

    [Fact]
    public void QueueOptionsUseBase64Encoding()
    {
        Assert.Equal(QueueMessageEncoding.Base64, WebhookQueue.QueueOptions.MessageEncoding);
    }

    [Fact]
    public async Task EnqueueAsync_PayloadAtLimit_IsStoredInlineWithoutBlobAccess()
    {
        var fx = new Fixture();
        var raw = new string('a', WebhookQueue.MaxInlinePayloadBytes);

        await fx.Queue.EnqueueAsync(Inline(raw, "https://discord.com/api/webhooks/1/x"));

        WebhookQueueMessage sent = JsonSerializer.Deserialize<WebhookQueueMessage>(Assert.Single(fx.EventSends).Body)!;
        Assert.Equal(Inline(raw, "https://discord.com/api/webhooks/1/x"), sent);
    }

    [Theory]
    [InlineData(8000, false)]
    [InlineData(10000, true)] // 20000 UTF-8 bytes but ~60 KB once JSON-escaped, so the envelope limit sends it to the blob
    [InlineData(16385, true)]
    public async Task EnqueueAsync_ThresholdIsMeasuredInUtf8Bytes(int twoByteCharCount, bool expectBlob)
    {
        var fx = new Fixture();
        var raw = new string('é', twoByteCharCount);
        var uploads = new List<string>();
        SetupUpload(fx, uploads);

        await fx.Queue.EnqueueAsync(Inline(raw));

        WebhookQueueMessage sent = JsonSerializer.Deserialize<WebhookQueueMessage>(Assert.Single(fx.EventSends).Body)!;
        Assert.Equal(expectBlob, sent.PayloadBlob is not null);
        Assert.Equal(expectBlob ? null : raw, sent.RawJson);
        Assert.Equal(expectBlob ? [raw] : [], uploads);
    }

    [Fact]
    public async Task EnqueueAsync_OversizedPayload_UploadsBlobAndEnqueuesReference()
    {
        var fx = new Fixture();
        var raw = new string('a', 10 * 1024 * 1024);
        var uploads = new List<string>();
        SetupUpload(fx, uploads);

        await fx.Queue.EnqueueAsync(Inline(raw, "https://discord.com/api/webhooks/1/x"), TimeSpan.FromSeconds(42));

        (var body, TimeSpan? delay) = Assert.Single(fx.EventSends);
        WebhookQueueMessage sent = JsonSerializer.Deserialize<WebhookQueueMessage>(body)!;
        Assert.Null(sent.RawJson);
        Assert.Matches(new Regex(@"^\d{8}/[0-9a-f]{32}\.json$"), sent.PayloadBlob);
        Assert.Equal(new Uri("https://discord.com/api/webhooks/1/x"), sent.WebhookUrl);
        Assert.Equal(TimeSpan.FromSeconds(42), delay);
        Assert.Equal([raw], uploads);
        Assert.DoesNotContain("discord.com", uploads[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueAsync_BlobBackedMessage_IsEnqueuedWithoutUpload()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = new("delivery-1", "push", null, "20261027/abc.json", null, ReceivedAt);

        await fx.Queue.EnqueueAsync(message, TimeSpan.FromMinutes(2));

        (var body, TimeSpan? delay) = Assert.Single(fx.EventSends);
        Assert.Equal(message, JsonSerializer.Deserialize<WebhookQueueMessage>(body));
        Assert.Equal(TimeSpan.FromMinutes(2), delay);
    }

    [Fact]
    public async Task EnqueueAsync_BlobUploadFails_ThrowsAndEnqueuesNothing()
    {
        var fx = new Fixture();
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        blob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "unavailable"));
        fx.Payloads.Setup(p => p.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);

        await Assert.ThrowsAsync<RequestFailedException>(
            () => fx.Queue.EnqueueAsync(Inline(new string('a', WebhookQueue.MaxInlinePayloadBytes + 1))));

        Assert.Empty(fx.EventSends);
    }

    [Fact]
    public async Task DeadLetterAsync_WritesEnvelopeToPoisonQueueKeepingPayloadBlob()
    {
        var fx = new Fixture();
        WebhookQueueMessage message = new("delivery-1", "push", null, "20261027/abc.json", null, ReceivedAt);

        await fx.Queue.DeadLetterAsync(message);

        Assert.Equal(message, JsonSerializer.Deserialize<WebhookQueueMessage>(Assert.Single(fx.PoisonSends)));
        Assert.Empty(fx.EventSends);
    }

    [Fact]
    public async Task LoadPayloadAsync_InlinePayload_NeedsNoStorageCall()
    {
        var fx = new Fixture();

        var payload = await fx.Queue.LoadPayloadAsync(Inline("{\"a\":1}"));

        Assert.Equal("{\"a\":1}", payload);
    }

    [Fact]
    public async Task LoadPayloadAsync_BlobPayload_ReturnsDownloadedContent()
    {
        var fx = new Fixture();
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        blob.Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobDownloadResult(content: BinaryData.FromString("{\"big\":true}")), Mock.Of<Response>()));
        fx.Payloads.Setup(p => p.GetBlobClient("20261027/abc.json")).Returns(blob.Object);

        var payload = await fx.Queue.LoadPayloadAsync(new("delivery-1", "push", null, "20261027/abc.json", null, ReceivedAt));

        Assert.Equal("{\"big\":true}", payload);
    }

    [Fact]
    public async Task LoadPayloadAsync_MissingBlob_ThrowsPayloadMissingException()
    {
        var fx = new Fixture();
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        blob.Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "not found"));
        fx.Payloads.Setup(p => p.GetBlobClient("20261027/gone.json")).Returns(blob.Object);

        PayloadMissingException ex = await Assert.ThrowsAsync<PayloadMissingException>(
            () => fx.Queue.LoadPayloadAsync(new("delivery-1", "push", null, "20261027/gone.json", null, ReceivedAt)));

        Assert.Equal("Payload blob 20261027/gone.json for delivery delivery-1 was not found.", ex.Message);
    }

    [Fact]
    public async Task LoadPayloadAsync_NeitherInlineNorBlob_ThrowsPayloadMissingException()
    {
        var fx = new Fixture();

        PayloadMissingException ex = await Assert.ThrowsAsync<PayloadMissingException>(
            () => fx.Queue.LoadPayloadAsync(new("delivery-1", "push", null, null, null, ReceivedAt)));

        Assert.Equal("Delivery delivery-1 has neither an inline payload nor a payload blob.", ex.Message);
    }

    private static void SetupUpload(Fixture fx, List<string> uploads)
    {
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        blob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<BinaryData, bool, CancellationToken>((content, overwrite, _) =>
            {
                Assert.False(overwrite);
                uploads.Add(content.ToString());
            })
            .ReturnsAsync(Response.FromValue<BlobContentInfo>(null!, Mock.Of<Response>()));
        fx.Payloads.Setup(p => p.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
    }
}
