using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub public イベントを Discord に通知する。</summary>
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
        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Uri.TryCreate(Event.Sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(Event.Sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Public,
            title: $"Published {Event.Repository.FullName} by {Event.Sender.Login}",
            url: Uri.TryCreate(Event.Repository.HtmlUrl, UriKind.Absolute, out var repoUrl) ? repoUrl : null,
            author: author);

        var key = $"{Event.Repository.FullName}-public-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
