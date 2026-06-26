using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>Discord Webhook API の送受信インターフェース。</summary>
public interface IDiscordClient
{
    /// <summary>メッセージを送信し、Discord が返したメッセージ ID を返す。</summary>
    Task<string> SendMessageAsync(string webhookUrl, DiscordMessage message);

    /// <summary>既存メッセージを編集する。</summary>
    Task EditMessageAsync(string webhookUrl, string messageId, DiscordMessage message);
}
