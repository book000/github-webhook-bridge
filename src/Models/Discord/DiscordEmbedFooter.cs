using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed のフッターを表すレコード</summary>
public record DiscordEmbedFooter(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("icon_url")] Uri? IconUrl = null);
