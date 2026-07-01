using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Record representing a Discord Embed</summary>
public record DiscordEmbed(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("color")] int? Color = null,
    [property: JsonPropertyName("author")] DiscordEmbedAuthor? Author = null,
    [property: JsonPropertyName("fields")] IList<DiscordEmbedField>? Fields = null,
    [property: JsonPropertyName("footer")] DiscordEmbedFooter? Footer = null,
    [property: JsonPropertyName("timestamp")] string? Timestamp = null);
