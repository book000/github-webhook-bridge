using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Embed フィールド。</summary>
public record DiscordEmbedField(
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("value")]  string Value,
    [property: JsonPropertyName("inline")] bool?  Inline = null);
