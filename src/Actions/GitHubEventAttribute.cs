namespace GitHubWebhookBridge.Actions;

/// <summary>
/// Attribute that binds a GitHub Webhook event handler class to an event name.
/// <para>
/// Example: <c>[GitHubEvent(WebhookEventType.PullRequest)]</c>
/// </para>
/// </summary>
/// <param name="eventName">The GitHub Webhook event name (lowercase snake_case).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute(string eventName) : Attribute
{
    /// <summary>
    /// Gets the event name specified via the <c>[GitHubEvent]</c> attribute.
    /// </summary>
    public string EventName { get; } = eventName;
}
