namespace GitHubWebhookBridge.Models.Discord;

/// <summary>Class defining Discord message flag constants</summary>
public static class DiscordMessageFlags
{
    /// <summary>Represents the suppress-notifications flag (4096)</summary>
    public const int SuppressNotifications = 1 << 12;
}
