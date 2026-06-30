using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>Discord Webhook API の送受信を定義するインターフェース</summary>
public interface IDiscordClient
{
    /// <summary>メッセージを送信し、Discord が返したメッセージ ID を返す</summary>
    /// <param name="webhookUrl">送信先 Discord Webhook URL</param>
    /// <param name="message">送信する Discord メッセージ</param>
    /// <returns>Discord が返したメッセージ ID 文字列</returns>
    Task<string> SendMessageAsync(Uri webhookUrl, DiscordMessage message);

    /// <summary>既存メッセージを編集する</summary>
    /// <param name="webhookUrl">対象メッセージの Discord Webhook URL</param>
    /// <param name="messageId">編集対象のメッセージ ID</param>
    /// <param name="message">編集後の Discord メッセージ内容</param>
    /// <returns>処理完了を表す <see cref="Task"/></returns>
    Task EditMessageAsync(Uri webhookUrl, string messageId, DiscordMessage message);
}
