namespace GitHubWebhookBridge.Managers;

/// <summary>ユーザーミュート判定インターフェース。</summary>
public interface IMuteManager
{
    Task EnsureLoadedAsync();
    bool IsMuted(long userId, string eventName, string? action);
}
