using System.Text.RegularExpressions;

namespace GitHubWebhookBridge.Utils;

/// <summary>SSRF countermeasure shared by the receiver and the queue worker.</summary>
internal static class WebhookUrlValidator
{
    private static readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord.com",
        "canary.discord.com",
        "ptb.discord.com",
        "discordapp.com",
        "canary.discordapp.com",
        "ptb.discordapp.com",
    };

    private static readonly Regex _webhookPath = new(
        "^/api(/v[0-9]+)?/webhooks/[0-9]{1,25}/[A-Za-z0-9._-]{1,256}/?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Allows only Discord webhook URLs: <c>https</c> on the default port without user info or a fragment,
    /// a host from the Discord allowlist (including the canary and ptb subdomains), and a
    /// <c>/api[/vN]/webhooks/{id}/{token}</c> path. A query string such as <c>thread_id</c> is allowed.
    /// </summary>
    /// <param name="url">The candidate webhook URL.</param>
    /// <returns><see langword="true"/> when the URL is an allowed Discord webhook URL.</returns>
    public static bool IsAllowedWebhookUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.Fragment.Length == 0
        && _allowedHosts.Contains(uri.Host)
        && _webhookPath.IsMatch(uri.AbsolutePath);
}
