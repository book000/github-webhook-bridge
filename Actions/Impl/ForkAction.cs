using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub fork イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class ForkAction(IDiscordClient d, Uri wu, string en, ForkEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : BaseAction<ForkEvent>(d, wu, en, e, c, u, l)
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
            color: EmbedColors.Fork,
            title: $"Forked {Event.Repository.FullName} by {Event.Sender.Login} to {Event.Forkee.FullName}",
            url: Event.Forkee.HtmlUrl,
            author: author);

        var key = $"{Event.Repository.FullName}-fork-{Event.Sender.Login}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
