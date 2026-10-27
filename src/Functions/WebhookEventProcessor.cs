using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// Delivers one queued webhook event to Discord. A Discord rate limit re-schedules the event;
/// failures that retrying cannot fix are moved to the poison queue; everything else propagates so
/// the Functions queue host retries it and finally poisons it.
/// </summary>
public sealed class WebhookEventProcessor(
    IWebhookQueue queue,
    IActionFactory actionFactory,
    IConfiguration config,
    TimeProvider clock,
    ILogger<WebhookEventProcessor> logger)
{
    /// <summary>Must equal <c>extensions.queues.maxDequeueCount</c> in <c>host.json</c>.</summary>
    internal const int MaxDequeueCount = 10;

    /// <summary>An event still rate-limited this long after receipt is moved to the poison queue.</summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    /// <summary>Lower bound of the re-schedule delay.</summary>
    internal static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound of the re-schedule delay before jitter.</summary>
    internal static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    /// <summary>Upper bound (exclusive) of the random jitter added to the re-schedule delay.</summary>
    internal static readonly TimeSpan MaxJitter = TimeSpan.FromSeconds(5);

    private readonly IWebhookQueue _queue = queue;
    private readonly IActionFactory _actionFactory = actionFactory;
    private readonly IConfiguration _config = config;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<WebhookEventProcessor> _logger = logger;

    /// <summary>Processes one dequeued event.</summary>
    /// <param name="message">The queued event.</param>
    /// <param name="dequeueCount">How many times the host has dequeued this message, including this attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the message may be deleted from the queue.</returns>
    public async Task ProcessAsync(WebhookQueueMessage message, long dequeueCount, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await DeliverAsync(message, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (dequeueCount >= MaxDequeueCount)
            {
                _logger.LogError(
                    ex,
                    "Webhook event dead-lettered: delivery failed {Attempts} times (DeliveryId={DeliveryId}, EventName={EventName})",
                    dequeueCount,
                    message.DeliveryId,
                    message.EventName);
            }

            throw;
        }
    }

    private async Task DeliverAsync(WebhookQueueMessage message, CancellationToken ct)
    {
        Uri webhookUrl;
        if (message.WebhookUrl is { } queuedUrl)
        {
            if (!WebhookUrlValidator.IsAllowedWebhookUrl(queuedUrl.AbsoluteUri))
            {
                await DeadLetterAsync(message, "webhook URL is not an allowed Discord webhook URL", null, ct).ConfigureAwait(false);
                return;
            }

            webhookUrl = queuedUrl;
        }
        else
        {
            webhookUrl = new Uri(_config["DISCORD_WEBHOOK_URL"]
                ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set"));
        }

        string rawJson;
        try
        {
            rawJson = await _queue.LoadPayloadAsync(message, ct).ConfigureAwait(false);
        }
        catch (PayloadMissingException ex)
        {
            await DeadLetterAsync(message, "payload is missing", ex, ct).ConfigureAwait(false);
            return;
        }

        IAction action;
        try
        {
            action = _actionFactory.GetAction(message.EventName, rawJson, webhookUrl);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            await DeadLetterAsync(message, "payload cannot be deserialized", ex, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            await action.RunAsync().ConfigureAwait(false);
        }
        catch (DiscordRateLimitException ex)
        {
            await RescheduleAsync(message, ex, ct).ConfigureAwait(false);
        }
        catch (NotImplementedException ex)
        {
            await DeadLetterAsync(message, "event has no handler in this build", ex, ct).ConfigureAwait(false);
        }
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Jitter spreads retries and is not security sensitive")]
    private async Task RescheduleAsync(WebhookQueueMessage message, DiscordRateLimitException rateLimit, CancellationToken ct)
    {
        if (_clock.GetUtcNow() - message.ReceivedAt > MaxAge)
        {
            await DeadLetterAsync(message, $"rate limited for more than {MaxAge.TotalHours:0} hours", rateLimit, ct).ConfigureAwait(false);
            return;
        }

        TimeSpan clamped = rateLimit.RetryAfter < MinDelay ? MinDelay : rateLimit.RetryAfter > MaxDelay ? MaxDelay : rateLimit.RetryAfter;
        TimeSpan delay = clamped + (MaxJitter * Random.Shared.NextDouble());
        _logger.LogWarning(
            "Discord rate limit: retrying in {DelaySeconds:0.0}s (scope={Scope}, global={IsGlobal}, DeliveryId={DeliveryId})",
            delay.TotalSeconds,
            rateLimit.Scope,
            rateLimit.IsGlobal,
            message.DeliveryId);
        await _queue.EnqueueAsync(message, delay, ct).ConfigureAwait(false);
    }

    private async Task DeadLetterAsync(WebhookQueueMessage message, string reason, Exception? cause, CancellationToken ct)
    {
        await _queue.DeadLetterAsync(message, ct).ConfigureAwait(false);
        _logger.LogError(
            cause,
            "Webhook event dead-lettered: {Reason} (DeliveryId={DeliveryId}, EventName={EventName})",
            reason,
            message.DeliveryId,
            message.EventName);
    }
}
