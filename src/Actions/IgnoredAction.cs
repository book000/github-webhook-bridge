using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// Handler for a delivery that is acknowledged on purpose without a Discord notification.
/// Because it lacks a <see cref="GitHubEventAttribute"/>, it is not registered in the <see cref="ActionFactory"/> registry.
/// </summary>
/// <param name="logger">Logger that records the ignored delivery.</param>
/// <param name="eventName">The GitHub event name, for example <c>issues</c>.</param>
/// <param name="action">The payload <c>action</c> that is ignored, for example <c>status_changed</c>.</param>
public sealed class IgnoredAction(ILogger<IgnoredAction> logger, string eventName, string action) : IAction
{
    /// <inheritdoc/>
    public Task RunAsync()
    {
        logger.LogInformation(
            "Ignored webhook event {EventName}.{Action}: no notification is sent for this action",
            eventName,
            action);
        return Task.CompletedTask;
    }
}
