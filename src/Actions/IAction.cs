namespace GitHubWebhookBridge.Actions;

/// <summary>Interface defining the common behavior of all GitHub Webhook event handlers.</summary>
public interface IAction
{
    /// <summary>Processes the Webhook event and notifies Discord.</summary>
    /// <returns>A <see cref="Task"/> representing completion of the processing.</returns>
    Task RunAsync();
}
