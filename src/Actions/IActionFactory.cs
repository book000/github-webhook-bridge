namespace GitHubWebhookBridge.Actions;

/// <summary>Interface defining the creation of an <see cref="IAction"/> from an event name.</summary>
public interface IActionFactory
{
    /// <summary>Creates and returns the appropriate <see cref="IAction"/> instance from an event name and raw JSON.</summary>
    /// <param name="eventName">The GitHub Webhook X-GitHub-Event header value (lowercase).</param>
    /// <param name="rawJson">The raw JSON string of the Webhook payload.</param>
    /// <param name="webhookUrl">The destination Discord Webhook URL.</param>
    /// <returns>The <see cref="IAction"/> instance corresponding to the event.</returns>
    IAction GetAction(string eventName, string rawJson, Uri webhookUrl);
}
