using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed の著者情報を表すクラス</summary>
public record DiscordEmbedAuthor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("icon_url")] Uri? IconUrl = null);
