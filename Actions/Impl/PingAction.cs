using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub ping イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PingAction(IDiscordClient d, Uri wu, string en, PingEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : BaseAction<PingEvent>(d, wu, en, e, c, u, l)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Ping,
            title: "Received a ping event",
            description: Event.Zen,
            fields: [
                new("Hook Type",    Event.Hook.Type,                                   true),
                new("Hook ID",      Event.HookId.ToString(System.Globalization.CultureInfo.InvariantCulture),                           true),
                new("Events",       (Event.Hook.Events?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),        true),
                new("Repository",   Event.Repository?.FullName ?? "N/A",              true),
                new("Sender",       Event.Sender?.Login ?? "N/A",                     true),
                new("Organization", Event.Organization?.Login ?? "N/A",               true),
            ]);

        var cacheKey = $"ping:{Event.Repository?.FullName ?? "N/A"}:{Event.Sender?.Login ?? "N/A"}:{Event.Organization?.Login ?? "N/A"}:{Event.Hook.Type}";
        await SendMessageAsync(cacheKey, new DiscordMessage(Embeds: [embed]));
    }
}
