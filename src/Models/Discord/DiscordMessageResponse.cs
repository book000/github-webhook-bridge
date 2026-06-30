using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord が ?wait=true で返すメッセージレスポンスを表すレコード</summary>
public record DiscordMessageResponse(
    [property: JsonPropertyName("id")] string Id);
