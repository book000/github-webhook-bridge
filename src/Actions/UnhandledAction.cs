namespace GitHubWebhookBridge.Actions;

/// <summary>
/// Fallback handler for unimplemented events.
/// Because it lacks a <see cref="GitHubEventAttribute"/>, it is not registered in the <see cref="ActionFactory"/> registry.
/// When invoked it always throws a <see cref="NotImplementedException"/>, which
/// <see cref="GitHubWebhookBridge.Functions.WebhookFunction"/> converts into an HTTP 406.
/// </summary>
public sealed class UnhandledAction(string eventName) : IAction
{
    /// <inheritdoc/>
    public Task RunAsync()
        => throw new NotImplementedException($"Event '{eventName}' is not implemented.");
}
