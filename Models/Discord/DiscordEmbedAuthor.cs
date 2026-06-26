using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed 著者情報。</summary>
public record DiscordEmbedAuthor(
    [property: JsonPropertyName("name")]     string  Name,
    [property: JsonPropertyName("url")]      string? Url     = null,
    [property: JsonPropertyName("icon_url")] string? IconUrl = null);
