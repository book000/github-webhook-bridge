using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub star イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class StarAction(IDiscordClient discord, Uri webhookUrl, string eventName, StarEvent starEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<StarEvent>(discord, webhookUrl, eventName, starEvent, cache, userMapManager, logger)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var titlePrefix = Event.Action == "created" ? "Starred" : "Unstarred";
        var color = Event.Action == "created" ? EmbedColors.Star : EmbedColors.Unstar;

        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Event.Sender.HtmlUrl,
            IconUrl: Event.Sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: $"{titlePrefix} {Event.Repository.FullName} by {Event.Sender.Login}",
            url: Event.Repository.HtmlUrl,
            author: author);

        var key = $"{Event.Repository.FullName}-star-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
