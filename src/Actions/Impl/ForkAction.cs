using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub fork イベントを Discord に通知する。</summary>
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
        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Uri.TryCreate(Event.Sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(Event.Sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Fork,
            title: $"Forked {Event.Repository.FullName} by {Event.Sender.Login} to {Event.Forkee.FullName}",
            url: Uri.TryCreate(Event.Forkee.HtmlUrl, UriKind.Absolute, out var forkeeUrl) ? forkeeUrl : null,
            author: author);

        var key = $"{Event.Repository.FullName}-fork-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
