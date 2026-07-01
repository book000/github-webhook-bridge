using System.Globalization;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub ping イベントを Discord に通知するクラス</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Ping)]
public sealed class PingAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PingAction> logger,
    Uri webhookUrl,
    string eventName,
    PingEvent pingEvent)
    : BaseAction<PingEvent>(discord, webhookUrl, eventName, pingEvent, cache, userMapManager, logger)
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
                new("Hook Type", Event.Hook?.Type.StringValue ?? "N/A", true),
                new("Hook ID", Event.HookId.ToString(CultureInfo.InvariantCulture), true),
                new("Events", (Event.Hook?.Events?.Count ?? 0).ToString(CultureInfo.InvariantCulture), true),
                new("Repository", Event.Repository?.FullName ?? "N/A", true),
                new("Sender", Event.Sender?.Login ?? "N/A", true),
                new("Organization", Event.Organization?.Login ?? "N/A", true),
            ]);

        var hookType = Event.Hook?.Type.StringValue ?? "N/A";
        var cacheKey = $"ping:{Event.Repository?.FullName ?? "N/A"}:{Event.Sender?.Login ?? "N/A"}:{Event.Organization?.Login ?? "N/A"}:{hookType}";
        await SendMessageAsync(cacheKey, new DiscordMessage(Embeds: [embed]));
    }
}
