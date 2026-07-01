using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Record representing the footer of a Discord Embed</summary>
public record DiscordEmbedFooter(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("icon_url")] Uri? IconUrl = null);
