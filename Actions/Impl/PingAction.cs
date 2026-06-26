using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub ping イベントを Discord に通知する。</summary>
public sealed class PingAction : BaseAction<PingEvent>
{
    /// <inheritdoc cref="BaseAction{TEvent}"/>
    public PingAction(IDiscordClient d, string wu, string en, PingEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var embed = EmbedHelper.CreateEmbed(
            eventName:   EventName,
            color:       EmbedColors.Ping,
            title:       "Received a ping event",
            description: Event.Zen,
            fields: [
                new("Hook ID", Event.HookId.ToString(), true),
            ]);

        await SendMessageAsync($"ping-{Event.HookId}", new DiscordMessage(Embeds: [embed]));
    }
}
