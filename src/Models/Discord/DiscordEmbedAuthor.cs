using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Record representing the author information of a Discord Embed</summary>
public record DiscordEmbedAuthor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("icon_url")] Uri? IconUrl = null);
