using System.Globalization;
using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// Abstract base class for all action handlers.
/// Provides Discord message sending and edit support backed by a 5-minute cache.
/// </summary>
public abstract class BaseAction<TEvent>(
    IDiscordClient discord,
    Uri webhookUrl,
    string eventName,
    TEvent @event,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger logger) : IAction
    where TEvent : WebhookEvent
{
    /// <summary>Gets the Discord Webhook API client.</summary>
    protected IDiscordClient Discord { get; } = discord;

    /// <summary>Gets the destination Discord Webhook URL.</summary>
    protected Uri WebhookUrl { get; } = webhookUrl;

    /// <summary>Gets the GitHub Webhook event name.</summary>
    protected string EventName { get; } = eventName;

    /// <summary>Gets the deserialized Webhook payload.</summary>
    protected TEvent Event { get; } = @event;

    /// <summary>Gets the GitHub-to-Discord user mapping manager.</summary>
    protected IGitHubUserMapManager UserMapManager { get; } = userMapManager;

    /// <summary>Gets the logger instance.</summary>
    protected ILogger Logger { get; } = logger;

    private readonly IMessageCacheService _cache = cache;

    /// <summary>Runs the event processing. Implemented by each subclass.</summary>
    public abstract Task RunAsync();

    /// <summary>
    /// Sends a message to Discord.
    /// If a message with the same key exists within the last 5 minutes, it is edited instead.
    /// The SuppressNotifications flag is applied to every message.
    /// </summary>
    /// <param name="key">Key string used to look up and store the cache entry.</param>
    /// <param name="message">The Discord message to send.</param>
    protected async Task SendMessageAsync(string key, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Add the SuppressNotifications flag (preserving existing flags).
        message = message with { Flags = message.Flags | DiscordMessageFlags.SuppressNotifications };

        CachedMessage? cached = await _cache.GetAsync(WebhookUrl, key);
        if (cached is not null)
        {
            try
            {
                await Discord.EditMessageAsync(WebhookUrl, cached.MessageId, message);
                return;
            }
            catch (Exception ex)
            {
                // On edit failure (e.g. the message was deleted), discard the cache entry and fall back to sending a new message.
                Logger.LogWarning(ex, "Failed to edit message {MessageId}, falling back to send.", cached.MessageId);
                await _cache.DeleteAsync(WebhookUrl, key);
            }
        }

        var messageId = await Discord.SendMessageAsync(WebhookUrl, message);
        await _cache.SetAsync(WebhookUrl, key, messageId);
    }

    /// <summary>
    /// Builds a Discord mention string from a list of GitHub user IDs.
    /// The sender is excluded. Filter out Team objects beforehand if present.
    /// </summary>
    /// <param name="senderId">The sender's GitHub user ID (excluded from mentions).</param>
    /// <param name="users">Collection of GitHub user IDs and login names to mention.</param>
    /// <returns>A space-separated Discord mention string, or an empty string when there are no targets.</returns>
    protected async Task<string> GetUsersMentionsAsync(
        long senderId,
        IEnumerable<(long Id, string Login)> users)
    {
        await UserMapManager.EnsureLoadedAsync();
        IEnumerable<string> mentions = users
            .Where(u => u.Id != senderId)
            .Select(u => UserMapManager.GetById(u.Id))
            .Where(discordId => discordId is not null)
            .Select(discordId => $"<@{discordId}>");
        return string.Join(" ", mentions);
    }

    /// <summary>
    /// Generates a unified diff between two texts (using DiffPlex InlineDiffBuilder).
    /// Uses +/-/space line prefixes. The caller should wrap it in a ```diff code block.
    /// </summary>
    /// <param name="oldText">The text before the change.</param>
    /// <param name="newText">The text after the change.</param>
    /// <param name="fileName">The file name shown in the diff header.</param>
    /// <returns>A diff string using +/-/space line prefixes.</returns>
    protected static string CreatePatch(string oldText, string newText, string fileName = "file")
    {
        DiffPaneModel diff = InlineDiffBuilder.Diff(oldText, newText);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"--- {fileName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"+++ {fileName}");

        foreach (DiffPiece? line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+",
                ChangeType.Deleted => "-",
                ChangeType.Unchanged => throw new NotImplementedException(),
                ChangeType.Imaginary => throw new NotImplementedException(),
                ChangeType.Modified => throw new NotImplementedException(),
                _ => " ",
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"{prefix} {line.Text}");
        }

        return sb.ToString();
    }
}
