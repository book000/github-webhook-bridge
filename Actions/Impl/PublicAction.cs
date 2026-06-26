using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub public イベントを Discord に通知する。</summary>
public sealed class PublicAction : BaseAction<PublicEvent>
{
    /// <inheritdoc cref="BaseAction{TEvent}"/>
    public PublicAction(IDiscordClient d, string wu, string en, PublicEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var author = new DiscordEmbedAuthor(
            Name:    Event.Sender.Login,
            Url:     Event.Sender.HtmlUrl,
            IconUrl: Event.Sender.AvatarUrl);

        var embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color:     EmbedColors.Public,
            title:     $"Published {Event.Repository.FullName} by {Event.Sender.Login}",
            url:       Event.Repository.HtmlUrl,
            author:    author);

        var key = $"{Event.Repository.FullName}-public-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
