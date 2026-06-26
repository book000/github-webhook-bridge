using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub push イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PushAction(IDiscordClient d, Uri wu, string en, PushEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : BaseAction<PushEvent>(d, wu, en, e, c, u, l)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        IList<Commit> allCommits = Event.Commits;
        if (allCommits.Count == 0) return;

        // refs/heads/ や refs/tags/ を除去して短いブランチ名にする
        var shortRef = Event.Ref
            .Replace("refs/heads/", "")
            .Replace("refs/tags/", "");

        // 先頭 5 件のみ表示する（TypeScript 実装との統一）
        const int CommitLimit = 5;
        var commits = allCommits.Take(CommitLimit).ToList();
        var description = GetDescription(commits, allCommits.Count);

        var author = new DiscordEmbedAuthor(
            Name: Event.Sender.Login,
            Url: Event.Sender.HtmlUrl,
            IconUrl: Event.Sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Push,
            title: $"[{Event.Repository.FullName}:{shortRef}] {allCommits.Count} new commit(s)",
            description: description,
            author: author);

        var key = $"{Event.Repository.FullName}:{Event.Ref}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    /// <summary>コミット一覧の説明文を生成する。</summary>
    /// <param name="commits">表示するコミット一覧（最大 5 件）。</param>
    /// <param name="totalCount">全コミット数。5 を超える場合は末尾に省略メッセージを付加する。</param>
    private static string GetDescription(List<Commit> commits, int totalCount)
    {
        var lines = commits.Select(c =>
        {
            var shortSha = c.Id.Length >= 7 ? c.Id[..7] : c.Id;
            // 複数行メッセージは最初の行のみ使用する
            var firstLine = c.Message.Contains('\n')
                ? c.Message.Split('\n', 2)[0]
                : c.Message;
            var shortMessage = firstLine.Length > 50
                ? $"{firstLine[..50]}..."
                : firstLine;
            return $"[`{shortSha}`]({c.Url}) {shortMessage} - {c.Author.Name}";
        }).ToList();

        if (totalCount > commits.Count)
            lines.Add($"...and {totalCount - commits.Count} more commit(s)");

        return string.Join("\n", lines);
    }
}
