using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// Record representing the minimal payload used for mute checks. Extracts only the sender ID and action fields.
/// </summary>
internal sealed record WebhookEnvelope(
    [property: JsonPropertyName("sender")] WebhookSender? Sender,
    [property: JsonPropertyName("action")] string? Action);

/// <summary>Minimal record representing sender information.</summary>
internal sealed record WebhookSender(
    // GitHub API's sender.id can be a large number, so long? is used.
    [property: JsonPropertyName("id")] long? Id);
