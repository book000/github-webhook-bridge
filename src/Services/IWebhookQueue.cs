using System.Diagnostics.CodeAnalysis;

namespace GitHubWebhookBridge.Services;

/// <summary>Persists accepted webhook events for asynchronous Discord delivery.</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A service that writes to a storage queue, not a collection type")]
public interface IWebhookQueue
{
    /// <summary>
    /// Stores <paramref name="message"/> in the event queue. A payload larger than 32 KiB (UTF-8) in
    /// <see cref="WebhookQueueMessage.RawJson"/> is uploaded to a blob first and replaced by a reference.
    /// </summary>
    /// <param name="message">The event to store.</param>
    /// <param name="visibilityDelay">Delay before the message becomes visible to the worker; <see langword="null"/> for none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes once the message is durably stored.</returns>
    Task EnqueueAsync(WebhookQueueMessage message, TimeSpan? visibilityDelay = null, CancellationToken ct = default);

    /// <summary>Copies <paramref name="message"/> to the poison queue so an operator can replay it.</summary>
    /// <param name="message">The event to isolate. Its <see cref="WebhookQueueMessage.PayloadBlob"/> reference is kept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes once the message is stored.</returns>
    Task DeadLetterAsync(WebhookQueueMessage message, CancellationToken ct = default);

    /// <summary>Returns the raw payload of <paramref name="message"/>, downloading it when it is stored in a blob.</summary>
    /// <param name="message">The queued event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw JSON payload.</returns>
    /// <exception cref="PayloadMissingException">The payload is neither inline nor present in blob storage.</exception>
    Task<string> LoadPayloadAsync(WebhookQueueMessage message, CancellationToken ct = default);
}
