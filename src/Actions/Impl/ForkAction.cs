using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub fork events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Fork)]
public sealed class ForkAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<ForkAction> logger,
    Uri webhookUrl,
    string eventName,
    ForkEvent forkEvent)
    : BaseAction<ForkEvent>(discord, webhookUrl, eventName, forkEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        if (Event.Sender is not { } sender || Event.Repository is not { } repo)
        {
            Logger.LogWarning("fork payload is missing sender or repository; skipping notification.");
            return;
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Fork,
            title: $"Forked {repo.FullName} by {sender.Login} to {Event.Forkee.FullName}",
            url: Uri.TryCreate(Event.Forkee.HtmlUrl, UriKind.Absolute, out Uri? forkeeUrl) ? forkeeUrl : null,
            author: author);

        var key = $"{repo.FullName}-fork-{sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
