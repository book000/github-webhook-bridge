using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Utils;

/// <summary>Helper class that builds Discord Embeds</summary>
public static class EmbedHelper
{
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 4096;
    private const int MaxFieldCount = 25;
    private const int MaxFieldNameLength = 256;
    private const int MaxFieldValueLength = 1024;
    private const int MaxFooterTextLength = 2048;
    private const int MaxAuthorNameLength = 256;
    private const int MaxTotalLength = 6000;

    private static readonly Uri _footerIconUri = new("https://i.imgur.com/PdvExHP.png");

    /// <summary>
    /// Truncates <paramref name="value"/> to at most <paramref name="maxLength"/> UTF-16 code units,
    /// ending with an ellipsis when something was cut. A cut that would split a surrogate pair drops the pair.
    /// </summary>
    /// <param name="value">The text to limit.</param>
    /// <param name="maxLength">The maximum length of the result.</param>
    /// <returns><paramref name="value"/> itself when it fits; otherwise the truncated text.</returns>
    internal static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        if (maxLength <= 0)
            return string.Empty;

        var keep = maxLength - 1;
        if (keep > 0 && char.IsHighSurrogate(value[keep - 1]))
            keep--;

        return $"{value[..keep]}…";
    }

    /// <summary>
    /// Builds a Discord Embed with a standard footer and timestamp.
    /// Equivalent to the TypeScript version's createEmbed().
    /// Every part is truncated to Discord's embed limits (title 256, description 4096, at most 25 fields,
    /// field name 256, field value 1024, author name 256, footer text 2048) and the embed as a whole to 6000
    /// characters: the description is shortened first, then trailing fields are dropped.
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
    {
        ArgumentNullException.ThrowIfNull(title);

        var limitedTitle = Truncate(title, MaxTitleLength);
        var limitedDescription = description is null ? null : Truncate(description, MaxDescriptionLength);
        DiscordEmbedAuthor? limitedAuthor = author is null
            ? null
            : author with { Name = Truncate(author.Name, MaxAuthorNameLength) };
        var limitedFields = fields?
            .Take(MaxFieldCount)
            .Select(f => f with { Name = Truncate(f.Name, MaxFieldNameLength), Value = Truncate(f.Value, MaxFieldValueLength) })
            .ToList();
        var footerText = Truncate($"Powered by book000/github-webhook-bridge ({eventName} event)", MaxFooterTextLength);

        int Total()
        {
            return limitedTitle.Length
                + (limitedDescription?.Length ?? 0)
                + (limitedAuthor?.Name.Length ?? 0)
                + footerText.Length
                + (limitedFields?.Sum(f => f.Name.Length + f.Value.Length) ?? 0);
        }

        if (Total() > MaxTotalLength && limitedDescription is not null)
            limitedDescription = Truncate(limitedDescription, Math.Max(0, limitedDescription.Length - (Total() - MaxTotalLength)));

        while (Total() > MaxTotalLength && limitedFields is { Count: > 0 })
            limitedFields.RemoveAt(limitedFields.Count - 1);

        return new(
            Title: limitedTitle,
            Description: limitedDescription,
            Url: url,
            Color: color,
            Author: limitedAuthor,
            Fields: limitedFields,
            Footer: new DiscordEmbedFooter(Text: footerText, IconUrl: _footerIconUri),
            Timestamp: DateTimeOffset.UtcNow.ToString("o"));
    }
}
