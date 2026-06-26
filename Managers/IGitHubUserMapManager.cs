namespace GitHubWebhookBridge.Managers;

/// <summary>GitHub ユーザー ID ↔ Discord ユーザー ID マッピングインターフェース。</summary>
public interface IGitHubUserMapManager
{
    Task EnsureLoadedAsync();
    string? Get(long githubUserId);
    Task<string?> GetFromUsernameAsync(string login);
}
