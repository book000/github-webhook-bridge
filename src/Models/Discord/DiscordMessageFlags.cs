namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Discord メッセージフラグの定数を定義するクラス。</summary>
public static class DiscordMessageFlags
{
    /// <summary>通知を抑制するフラグ（4096）を示す。</summary>
    public const int SuppressNotifications = 1 << 12;
}
