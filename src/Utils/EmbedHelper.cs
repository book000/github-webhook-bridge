using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Utils;

/// <summary>Helper class that builds Discord Embeds</summary>
public static class EmbedHelper
{
    private static readonly Uri _footerIconUri = new("https://i.imgur.com/PdvExHP.png");

    /// <summary>
    /// Builds a Discord Embed with a standard footer and timestamp.
    /// Equivalent to the TypeScript version's createEmbed()
    /// </summary>
    /// <param name="eventName">GitHub Webhook event name shown in the footer</param>
    /// <param name="color">Embed sidebar color (hexadecimal integer)</param>
    /// <param name="title">Embed title string</param>
    /// <param name="description">Embed body text (optional)</param>
    /// <param name="url">Link target URL for the title (optional)</param>
    /// <param name="author">Embed author information (optional)</param>
    /// <param name="fields">List of Embed fields (optional)</param>
    /// <returns>A <see cref="DiscordEmbed"/> instance with a footer and timestamp attached</returns>
    public static DiscordEmbed CreateEmbed(
        string eventName,
        int color,
        string title,
        string? description = null,
        Uri? url = null,
        DiscordEmbedAuthor? author = null,
        IList<DiscordEmbedField>? fields = null)
        => new(
            Title: title,
            Description: description,
            Url: url,
            Color: color,
            Author: author,
            Fields: fields,
            Footer: new DiscordEmbedFooter(
                Text: $"Powered by book000/github-webhook-bridge ({eventName} event)",
                IconUrl: _footerIconUri),
            Timestamp: DateTimeOffset.UtcNow.ToString("o"));
}
