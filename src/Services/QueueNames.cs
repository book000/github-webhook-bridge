namespace GitHubWebhookBridge.Services;

/// <summary>Names of the storage resources used for webhook event delivery. Provisioned by <c>infra/main.bicep</c>.</summary>
public static class QueueNames
{
    /// <summary>Queue holding accepted webhook events awaiting Discord delivery.</summary>
    public const string Events = "webhook-events";

    /// <summary>Queue holding events that must be replayed by an operator.</summary>
    public const string Poison = "webhook-events-poison";

    /// <summary>Blob container holding payloads too large for a queue message.</summary>
    public const string PayloadContainer = "webhook-payloads";
}
