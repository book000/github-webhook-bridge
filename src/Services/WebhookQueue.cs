using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// Azure Storage implementation of <see cref="IWebhookQueue"/>.
/// Queues and the payload container are provisioned by infrastructure; no create calls are made here.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A service that writes to a storage queue, not a collection type")]
public sealed class WebhookQueue : IWebhookQueue
{
    /// <summary>Largest UTF-8 payload kept inline.</summary>
    internal const int MaxInlinePayloadBytes = 32768;

    // JSON escaping can inflate the payload up to 6x, so the serialized envelope is bounded too.
    // 48 KiB of JSON is 64 KiB once Base64-encoded, the queue message limit.
    internal const int MaxInlineEnvelopeBytes = 49152;

    // The Functions queue trigger expects Base64 bodies; the SDK default is no encoding.
    internal static readonly QueueClientOptions QueueOptions = new() { MessageEncoding = QueueMessageEncoding.Base64 };

    private readonly QueueClient _events;
    private readonly QueueClient _poison;
    private readonly BlobContainerClient _payloads;

    /// <summary>Initializes a new instance of the <see cref="WebhookQueue"/> class.</summary>
    /// <param name="config">Configuration providing <c>AzureWebJobsStorage</c>.</param>
    public WebhookQueue(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var connStr = config["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");
        _events = new QueueClient(connStr, QueueNames.Events, QueueOptions);
        _poison = new QueueClient(connStr, QueueNames.Poison, QueueOptions);
        _payloads = new BlobContainerClient(connStr, QueueNames.PayloadContainer);
    }

    internal WebhookQueue(QueueClient events, QueueClient poison, BlobContainerClient payloads)
    {
        _events = events;
        _poison = poison;
        _payloads = payloads;
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(WebhookQueueMessage message, TimeSpan? visibilityDelay = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = JsonSerializer.Serialize(message);
        if (message.RawJson is { } raw
            && (Encoding.UTF8.GetByteCount(raw) > MaxInlinePayloadBytes || Encoding.UTF8.GetByteCount(body) > MaxInlineEnvelopeBytes))
        {
            var blobName = $"{DateTimeOffset.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}.json";
            await _payloads.GetBlobClient(blobName)
                .UploadAsync(BinaryData.FromString(raw), overwrite: false, ct).ConfigureAwait(false);
            body = JsonSerializer.Serialize(message with { RawJson = null, PayloadBlob = blobName });
        }

        await _events.SendMessageAsync(body, visibilityDelay, timeToLive: null, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeadLetterAsync(WebhookQueueMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _poison.SendMessageAsync(JsonSerializer.Serialize(message), visibilityTimeout: null, timeToLive: null, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> LoadPayloadAsync(WebhookQueueMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.RawJson is { } raw)
            return raw;

        if (message.PayloadBlob is not { } blobName)
            throw new PayloadMissingException($"Delivery {message.DeliveryId} has neither an inline payload nor a payload blob.");

        try
        {
            BlobDownloadResult result = await _payloads.GetBlobClient(blobName).DownloadContentAsync(ct).ConfigureAwait(false);
            return result.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new PayloadMissingException($"Payload blob {blobName} for delivery {message.DeliveryId} was not found.");
        }
    }
}
