namespace GitHubWebhookBridge.Services;

/// <summary>Discord メッセージ ID の 5 分間キャッシュインターフェース。</summary>
public interface IMessageCacheService
{
    Task<CachedMessage?> GetAsync(Uri webhookUrl, string key);
    Task SetAsync(Uri webhookUrl, string key, string messageId);
    /// <summary>指定キーのキャッシュエントリを削除する。編集失敗時のフォールバック用。</summary>
    Task DeleteAsync(Uri webhookUrl, string key);
}

/// <summary>キャッシュされたメッセージ情報。</summary>
public record CachedMessage(string MessageId);
