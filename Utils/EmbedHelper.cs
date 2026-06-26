using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Utils;

/// <summary>Discord Embed 生成ヘルパー。</summary>
public static class EmbedHelper
{
    private static readonly Uri _footerIconUri = new("https://i.imgur.com/PdvExHP.png");

    /// <summary>
    /// 標準フッター・タイムスタンプ付きの Discord Embed を生成する。
    /// TypeScript 版の createEmbed() に相当。
    /// </summary>
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
