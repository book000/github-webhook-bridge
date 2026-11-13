namespace GitHubWebhookBridge.Utils;

/// <summary>Text helpers shared by actions that embed user-authored text.</summary>
public static class TextHelper
{
    private const string Ellipsis = "...";
    private const string Fence = "```";

    /// <summary>
    /// Keeps at most <paramref name="maxLength"/> UTF-16 code units of <paramref name="value"/>, then appends an ellipsis.
    /// A trailing lone high surrogate is dropped, and a code fence left open by the cut is closed after the ellipsis.
    /// Fence detection counts lines starting (after up to 3 spaces) with three backticks; tilde fences are not handled.
    /// </summary>
    /// <param name="value">The text to truncate.</param>
    /// <param name="maxLength">The maximum number of UTF-16 code units to keep (before the ellipsis and any closing fence).</param>
    /// <returns><paramref name="value"/> unchanged when it fits; otherwise the truncated text.</returns>
    public static string Truncate(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length <= maxLength) return value;

        var end = maxLength;
        if (end > 0 && char.IsHighSurrogate(value[end - 1])) end--;

        var kept = value[..end];
        return HasOpenFence(kept)
            ? $"{kept}{Ellipsis}\n{Fence}"
            : $"{kept}{Ellipsis}";
    }

    private static bool HasOpenFence(string text)
    {
        var open = false;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart(' ');
            if (trimmed.StartsWith(Fence, StringComparison.Ordinal) && line.Length - trimmed.Length <= 3)
            {
                open = !open;
            }
        }

        return open;
    }
}
