namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord メッセージフラグ定数。</summary>
public static class DiscordMessageFlags
{
    /// <summary>通知を抑制するフラグ（4096）。</summary>
    public const int SuppressNotifications = 1 << 12;
}
