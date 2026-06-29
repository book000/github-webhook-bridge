namespace GitHubWebhookBridge.Services;

/// <summary>Discord メッセージ ID の 5 分間キャッシュインターフェース。</summary>
public interface IMessageCacheService
{
    /// <summary>指定キーに対応するキャッシュエントリを取得する。</summary>
    /// <param name="webhookUrl">キャッシュを区別する Webhook URL。</param>
    /// <param name="key">メッセージを識別するキー文字列。</param>
    /// <returns>キャッシュされたメッセージ情報。存在しない、または期限切れの場合は null。</returns>
    Task<CachedMessage?> GetAsync(Uri webhookUrl, string key);

    /// <summary>指定キーでメッセージ ID をキャッシュに保存する。</summary>
    /// <param name="webhookUrl">キャッシュを区別する Webhook URL。</param>
    /// <param name="key">メッセージを識別するキー文字列。</param>
    /// <param name="messageId">保存する Discord メッセージ ID。</param>
    /// <returns>処理完了を表す <see cref="Task"/>。</returns>
    Task SetAsync(Uri webhookUrl, string key, string messageId);

    /// <summary>指定キーのキャッシュエントリを削除する。編集失敗時のフォールバック用。</summary>
    /// <param name="webhookUrl">キャッシュを区別する Webhook URL。</param>
    /// <param name="key">削除対象のキー文字列。</param>
    /// <returns>処理完了を表す <see cref="Task"/>。</returns>
    Task DeleteAsync(Uri webhookUrl, string key);
}

/// <summary>キャッシュされたメッセージ情報。</summary>
public record CachedMessage(string MessageId);
