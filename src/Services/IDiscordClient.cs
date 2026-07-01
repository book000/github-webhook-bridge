using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>Interface defining sending and receiving over the Discord Webhook API</summary>
public interface IDiscordClient
{
    /// <summary>Sends a message and returns the message ID that Discord returned</summary>
    /// <param name="webhookUrl">Destination Discord Webhook URL</param>
    /// <param name="message">Discord message to send</param>
    /// <returns>Message ID string returned by Discord</returns>
    Task<string> SendMessageAsync(Uri webhookUrl, DiscordMessage message);

    /// <summary>Edits an existing message</summary>
    /// <param name="webhookUrl">Discord Webhook URL of the target message</param>
    /// <param name="messageId">ID of the message to edit</param>
    /// <param name="message">Edited Discord message content</param>
    /// <returns>A <see cref="Task"/> representing completion of the operation</returns>
    Task EditMessageAsync(Uri webhookUrl, string messageId, DiscordMessage message);
}
