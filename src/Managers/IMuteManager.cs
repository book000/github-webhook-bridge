namespace GitHubWebhookBridge.Managers;

/// <summary>Interface defining mute determination for users.</summary>
public interface IMuteManager
{
    /// <summary>Loads the data on the first successful call (prevents double initialization). If retrieval fails, empty data is used and retrieval is retried after a cool-down.</summary>
    /// <returns>A <see cref="Task"/> representing completion.</returns>
    Task EnsureLoadedAsync();

    /// <summary>Returns whether the user is muted for the specified event.</summary>
    /// <param name="userId">The GitHub user ID to evaluate.</param>
    /// <param name="eventName">The GitHub webhook event name.</param>
    /// <param name="action">The event's action type (optional).</param>
    /// <returns><see langword="true"/> if muted; otherwise <see langword="false"/>.</returns>
    bool IsMuted(long userId, string eventName, string? action);
}
