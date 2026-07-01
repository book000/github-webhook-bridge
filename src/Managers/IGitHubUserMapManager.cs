namespace GitHubWebhookBridge.Managers;

/// <summary>Interface defining the mapping between GitHub user IDs and Discord user IDs.</summary>
public interface IGitHubUserMapManager
{
    /// <summary>Loads the data only on the first call (prevents double initialization).</summary>
    /// <returns>A <see cref="Task"/> representing completion.</returns>
    Task EnsureLoadedAsync();

    /// <summary>Gets the Discord user ID from a GitHub user ID.</summary>
    /// <param name="githubUserId">The GitHub user ID to look up.</param>
    /// <returns>The corresponding Discord user ID, or <see langword="null"/> if no mapping exists.</returns>
    string? GetById(long githubUserId);

    /// <summary>Resolves a numeric ID from a username via the GitHub API and looks it up in the map.</summary>
    /// <param name="login">The GitHub login name to look up.</param>
    /// <returns>The corresponding Discord user ID, or <see langword="null"/> if no mapping exists.</returns>
    Task<string?> GetFromUsernameAsync(string login);
}
