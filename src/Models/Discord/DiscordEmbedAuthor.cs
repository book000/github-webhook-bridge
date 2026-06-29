using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed 著者情報。</summary>
public record DiscordEmbedAuthor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("icon_url")] Uri? IconUrl = null);
