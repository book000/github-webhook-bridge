using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed オブジェクト。</summary>
public record DiscordEmbed(
    [property: JsonPropertyName("title")]       string?                  Title       = null,
    [property: JsonPropertyName("description")] string?                  Description = null,
    [property: JsonPropertyName("url")]         string?                  Url         = null,
    [property: JsonPropertyName("color")]       int?                     Color       = null,
    [property: JsonPropertyName("author")]      DiscordEmbedAuthor?      Author      = null,
    [property: JsonPropertyName("fields")]      List<DiscordEmbedField>? Fields      = null,
    [property: JsonPropertyName("footer")]      DiscordEmbedFooter?      Footer      = null,
    [property: JsonPropertyName("timestamp")]   string?                  Timestamp   = null);
