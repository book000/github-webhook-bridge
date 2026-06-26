namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord メッセージフラグ定数。</summary>
public static class DiscordMessageFlags
{
    /// <summary>通知を抑制するフラグ (1 &lt;&lt; 12 = 4096)。</summary>
    public const int SuppressNotifications = 1 << 12;
}
