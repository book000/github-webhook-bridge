namespace GitHubWebhookBridge.Utils;

/// <summary>SSRF countermeasure shared by the receiver and the queue worker.</summary>
internal static class WebhookUrlValidator
{
    /// <summary>Allows only discord.com / discordapp.com webhook URL prefixes.</summary>
    /// <param name="url">The candidate webhook URL.</param>
    /// <returns><see langword="true"/> when the URL starts with an allowed prefix.</returns>
    public static bool IsAllowedWebhookUrl(string url)
        => url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
}
