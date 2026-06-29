using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub public イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PublicAction(IDiscordClient discord, Uri webhookUrl, string eventName, PublicEvent publicEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<PublicEvent>(discord, webhookUrl, eventName, publicEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Event.Sender.HtmlUrl,
            IconUrl: Event.Sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Public,
            title: $"Published {Event.Repository.FullName} by {Event.Sender.Login}",
            url: Event.Repository.HtmlUrl,
            author: author);

        var key = $"{Event.Repository.FullName}-public-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
