using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="WebhookUrlValidator"/>, the SSRF allowlist for <c>?url=</c>.</summary>
public class WebhookUrlValidatorTests
{
    /// <summary>Discord webhook URLs, including canary / ptb hosts, versioned paths and a thread query, are accepted.</summary>
    [Theory]
    [InlineData("https://discord.com/api/webhooks/1/x")]
    [InlineData("https://discordapp.com/api/webhooks/222/bbb")]
    [InlineData("https://DISCORD.com/api/webhooks/123456/aB-_.9")]
    [InlineData("https://canary.discord.com/api/webhooks/1/x")]
    [InlineData("https://ptb.discord.com/api/webhooks/1/x")]
    [InlineData("https://canary.discordapp.com/api/webhooks/1/x")]
    [InlineData("https://ptb.discordapp.com/api/v10/webhooks/1/x?thread_id=5")]
    [InlineData("https://discord.com/api/webhooks/1/x/")]
    public void AcceptsDiscordWebhookUrl(string candidate)
    {
        Assert.True(WebhookUrlValidator.IsAllowedWebhookUrl(candidate));
        Assert.True(WebhookUrlValidator.IsAllowedWebhookUrl(new Uri(candidate).AbsoluteUri));
    }

    /// <summary>URLs that are not a plain Discord webhook are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://discord.com/api/webhooks/1/x")]
    [InlineData("https://discord.com@evil.example/api/webhooks/1/x")]
    [InlineData("https://user:pass@discord.com/api/webhooks/1/x")]
    [InlineData("https://evil.discord.com.example/api/webhooks/1/x")]
    [InlineData("https://x.discord.com/api/webhooks/1/x")]
    [InlineData("https://evil.example/api/webhooks/1/x")]
    [InlineData("https://notdiscord.com/api/webhooks/1/x")]
    [InlineData("https://discord.com/api/webhooks/abc/x")]
    [InlineData("https://discord.com/api/webhooks/١٢٣/x")]
    [InlineData("https://discord.com/api/webhooks/1/")]
    [InlineData("https://discord.com/api/webhooks/1/x/y")]
    [InlineData("https://discord.com/api/webhooks/1/x%2Fy")]
    [InlineData("https://discord.com/api/webhooks/1/..")]
    [InlineData("https://discord.com/api/webhooks/1/%2e%2e")]
    [InlineData("https://discord.com/api/webhooks/")]
    [InlineData("https://discord.com/api/webhooks")]
    [InlineData("https://discord.com:8443/api/webhooks/1/x")]
    [InlineData("https://discord.com/api/webhooks/1/x#frag")]
    [InlineData("https://discord.com/other/api/webhooks/1/x")]
    public void RejectsNonDiscordWebhookUrl(string candidate)
        => Assert.False(WebhookUrlValidator.IsAllowedWebhookUrl(candidate));
}
