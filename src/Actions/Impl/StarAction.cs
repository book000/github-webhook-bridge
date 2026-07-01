using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub star イベントを Discord に通知するクラス</summary>
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
        if (Event.Sender is not { } sender || Event.Repository is not { } repo)
        {
            Logger.LogWarning("star payload is missing sender or repository; skipping notification.");
            return;
        }

        var titlePrefix = Event.Action == "created" ? "Starred" : "Unstarred";
        var color = Event.Action == "created" ? EmbedColors.Star : EmbedColors.Unstar;

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: $"{titlePrefix} {repo.FullName} by {sender.Login}",
            url: Uri.TryCreate(repo.HtmlUrl, UriKind.Absolute, out Uri? repoUrl) ? repoUrl : null,
            author: author);

        var key = $"{repo.FullName}-star-{sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
