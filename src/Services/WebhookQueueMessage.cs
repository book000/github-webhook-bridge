namespace GitHubWebhookBridge.Services;

/// <summary>
/// Envelope stored in the webhook event queue. Exactly one of <paramref name="RawJson"/> and
/// <paramref name="PayloadBlob"/> is set.
/// </summary>
/// <param name="DeliveryId">The <c>X-GitHub-Delivery</c> value, or a generated GUID when GitHub did not send one.</param>
/// <param name="EventName">The normalized lowercase GitHub event name.</param>
/// <param name="RawJson">The validated raw payload, or <see langword="null"/> when it is stored in <paramref name="PayloadBlob"/>.</param>
/// <param name="PayloadBlob">The name of the blob holding the payload when it exceeds the inline limit.</param>
/// <param name="WebhookUrl">The <c>?url=</c> destination, or <see langword="null"/> to use <c>DISCORD_WEBHOOK_URL</c> at processing time.</param>
/// <param name="ReceivedAt">The UTC receipt time, preserved across rate-limit re-enqueues.</param>
public sealed record WebhookQueueMessage(
    string DeliveryId,
    string EventName,
    string? RawJson,
    string? PayloadBlob,
    Uri? WebhookUrl,
    DateTimeOffset ReceivedAt);
