using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord Webhook に送信するメッセージ。</summary>
public record DiscordMessage(
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("embeds")] IList<DiscordEmbed>? Embeds = null,
    [property: JsonPropertyName("flags")] int Flags = 0);
