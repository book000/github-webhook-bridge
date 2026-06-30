using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub star イベントを Discord に通知するクラス。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Star)]
public sealed class StarAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<StarAction> logger,
    Uri webhookUrl,
    string eventName,
    StarEvent starEvent)
    : BaseAction<StarEvent>(discord, webhookUrl, eventName, starEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var titlePrefix = Event.Action == "created" ? "Starred" : "Unstarred";
        var color = Event.Action == "created" ? EmbedColors.Star : EmbedColors.Unstar;

        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Uri.TryCreate(Event.Sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(Event.Sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: $"{titlePrefix} {Event.Repository.FullName} by {Event.Sender.Login}",
            url: Uri.TryCreate(Event.Repository.HtmlUrl, UriKind.Absolute, out var repoUrl) ? repoUrl : null,
            author: author);

        var key = $"{Event.Repository.FullName}-star-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
