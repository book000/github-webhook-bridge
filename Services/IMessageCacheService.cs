namespace GitHubWebhookBridge.Services;

/// <summary>Discord メッセージ ID の 5 分間キャッシュインターフェース。</summary>
public interface IMessageCacheService
{
    Task<CachedMessage?> GetAsync(string webhookUrl, string key);
    Task SetAsync(string webhookUrl, string key, string messageId);
}

/// <summary>キャッシュされたメッセージ情報。</summary>
public record CachedMessage(string MessageId);
