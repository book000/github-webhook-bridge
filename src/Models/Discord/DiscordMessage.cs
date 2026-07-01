using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Record representing a message sent to a Discord Webhook</summary>
public record DiscordMessage(
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("embeds")] IList<DiscordEmbed>? Embeds = null,
    [property: JsonPropertyName("flags")] int Flags = 0);
