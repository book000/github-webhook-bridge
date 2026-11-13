using System.Globalization;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Extensions;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub ping events.</summary>
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
    private const int MaxFieldValueLength = 1024;

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        List<DiscordEmbedField> fields = [
            new("Hook Type", Event.Hook?.Type.StringValue ?? "N/A", true),
            new("Hook ID", Event.HookId.ToString(CultureInfo.InvariantCulture), true),
        ];
        if (Event.Hook is { } hook)
        {
            fields.Add(new("Active", hook.Active ? "Yes" : "No", true));
        }

        fields.Add(new("Events", FormatEvents(Event.Hook?.Events), false));
        fields.Add(new("Repository", Event.Repository?.FullName ?? "N/A", true));
        fields.Add(new("Sender", Event.Sender?.Login ?? "N/A", true));
        fields.Add(new("Organization", Event.Organization?.Login ?? "N/A", true));

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Ping,
            title: "Received a ping event",
            description: Event.Zen,
            fields: fields);

        var hookType = Event.Hook?.Type.StringValue ?? "N/A";
        var cacheKey = $"ping:{Event.Repository?.FullName ?? "N/A"}:{Event.Sender?.Login ?? "N/A"}:{Event.Organization?.Login ?? "N/A"}:{hookType}";
        await SendMessageAsync(cacheKey, new DiscordMessage(Embeds: [embed]));
    }

    private static string FormatEvents(IEnumerable<StringEnum<AppEvent>>? events)
    {
        var joined = string.Join(", ", (events ?? []).Select(e => e.StringValue));
        if (joined.Length == 0)
        {
            return "N/A";
        }

        return joined.Length > MaxFieldValueLength ? string.Concat(joined.AsSpan(0, MaxFieldValueLength - 1), "…") : joined;
    }
}
