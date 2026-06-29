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
    /// <param name="eventName">フッターに表示する GitHub Webhook イベント名。</param>
    /// <param name="color">Embed のサイドバー色（16 進数整数）。</param>
    /// <param name="title">Embed のタイトル文字列。</param>
    /// <param name="description">Embed の本文テキスト（省略可）。</param>
    /// <param name="url">タイトルのリンク先 URL（省略可）。</param>
    /// <param name="author">Embed の著者情報（省略可）。</param>
    /// <param name="fields">Embed のフィールド一覧（省略可）。</param>
    /// <returns>フッターとタイムスタンプが付与された <see cref="DiscordEmbed"/> インスタンス。</returns>
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
