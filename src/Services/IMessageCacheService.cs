namespace GitHubWebhookBridge.Services;

/// <summary>Interface defining functionality that caches Discord message IDs for 5 minutes</summary>
public interface IMessageCacheService
{
    /// <summary>Gets the cache entry corresponding to the specified key</summary>
    /// <param name="webhookUrl">Webhook URL that distinguishes the cache</param>
    /// <param name="key">Key string identifying the message</param>
    /// <returns>Cached message information, or <see langword="null"/> if it does not exist or has expired</returns>
    Task<CachedMessage?> GetAsync(Uri webhookUrl, string key);

    /// <summary>Stores a message ID in the cache under the specified key</summary>
    /// <param name="webhookUrl">Webhook URL that distinguishes the cache</param>
    /// <param name="key">Key string identifying the message</param>
    /// <param name="messageId">Discord message ID to store</param>
    /// <returns>A <see cref="Task"/> representing completion of the operation</returns>
    Task SetAsync(Uri webhookUrl, string key, string messageId);

    /// <summary>Deletes the cache entry for the specified key. Used as a fallback on edit failure</summary>
    /// <param name="webhookUrl">Webhook URL that distinguishes the cache</param>
    /// <param name="key">Key string to delete</param>
    /// <returns>A <see cref="Task"/> representing completion of the operation</returns>
    Task DeleteAsync(Uri webhookUrl, string key);

    /// <summary>
    /// Deletes every entry that was last written more than the purge retention (1 hour, well beyond the 5-minute TTL) ago.
    /// Entries are otherwise only removed when the same key is read again.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entries actually deleted</returns>
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken);
}

/// <summary>Record representing cached message information</summary>
public record CachedMessage(string MessageId);
