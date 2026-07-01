using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Record representing the message response Discord returns with ?wait=true</summary>
public record DiscordMessageResponse(
    [property: JsonPropertyName("id")] string Id);
