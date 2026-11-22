using System.Diagnostics.CodeAnalysis;
using GitHubWebhookBridge.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Functions;

/// <summary>Timer trigger that reclaims expired message-cache rows, which are otherwise only removed when the same key is read again.</summary>
public class PurgeMessageCacheFunction(IMessageCacheService cache, ILogger<PurgeMessageCacheFunction> logger)
{
    private readonly IMessageCacheService _cache = cache;
    private readonly ILogger<PurgeMessageCacheFunction> _logger = logger;

    /// <summary>Deletes expired message-cache rows daily at 18:00 UTC (03:00 JST).</summary>
    /// <param name="timer">Timer schedule information (unused).</param>
    /// <param name="cancellationToken">Cancellation token signalled on host shutdown.</param>
    /// <returns>A <see cref="Task"/> that completes when the purge has finished.</returns>
    [Function("PurgeMessageCache")]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The timer trigger binding requires the TimerInfo parameter")]
    public async Task RunAsync(
        [TimerTrigger("0 0 18 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var deleted = await _cache.DeleteExpiredAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Purged {Count} expired message cache entities", deleted);
    }
}
