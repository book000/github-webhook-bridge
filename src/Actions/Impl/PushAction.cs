using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using OctokitCommit = Octokit.Webhooks.Models.PushEvent.Commit;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub push イベントを Discord に通知するクラス</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Push)]
public sealed class PushAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PushAction> logger,
    Uri webhookUrl,
    string eventName,
    PushEvent pushEvent)
    : BaseAction<PushEvent>(discord, webhookUrl, eventName, pushEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        IReadOnlyList<OctokitCommit> allCommits = Event.Commits;
        if (allCommits.Count == 0) return;

        // refs/heads/ や refs/tags/ を除去して短いブランチ名にする
        var shortRef = Event.Ref
            .Replace("refs/heads/", string.Empty)
            .Replace("refs/tags/", string.Empty);

        // 先頭 5 件のみ表示する
        const int CommitLimit = 5;
        var commits = allCommits.Take(CommitLimit).ToList();
        var description = GetDescription(commits, allCommits.Count);

        if (Event.Sender is not { } sender || Event.Repository is not { } repo)
        {
            Logger.LogWarning("push payload is missing sender or repository; skipping notification.");
            return;
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: EmbedColors.Push,
            title: $"[{repo.FullName}:{shortRef}] {allCommits.Count} new commit(s)",
            description: description,
            author: author);

        var key = $"{repo.FullName}:{Event.Ref}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    /// <summary>コミット一覧の説明文を生成する</summary>
    /// <param name="commits">表示するコミット一覧（最大 5 件）</param>
    /// <param name="totalCount">全コミット数。5 を超える場合は末尾に省略メッセージを付加する</param>
    private static string GetDescription(List<OctokitCommit> commits, int totalCount)
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
