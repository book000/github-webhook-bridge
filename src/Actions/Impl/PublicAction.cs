using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub public events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Public)]
public sealed class PublicAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PublicAction> logger,
    Uri webhookUrl,
    string eventName,
    PublicEvent publicEvent)
    : BaseAction<PublicEvent>(discord, webhookUrl, eventName, publicEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        if (Event.Sender is not { } sender || Event.Repository is not { } repo)
        {
            Logger.LogWarning("public payload is missing sender or repository; skipping notification.");
            return;
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Public,
            title: $"Published {repo.FullName} by {sender.Login}",
            url: Uri.TryCreate(repo.HtmlUrl, UriKind.Absolute, out Uri? repoUrl) ? repoUrl : null,
            author: author);

        var key = $"{repo.FullName}-public-{sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
