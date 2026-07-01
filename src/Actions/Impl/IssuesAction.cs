using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Models;
using IssuesEventChanges = Octokit.Webhooks.Models.IssuesEvent.Changes;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub issues イベントを Discord に通知するクラス</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Issues)]
public sealed class IssuesAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<IssuesAction> logger,
    Uri webhookUrl,
    string eventName,
    IssuesEvent issuesEvent)
    : BaseAction<IssuesEvent>(discord, webhookUrl, eventName, issuesEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Issue issue = Event.Issue;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("issues payload is missing repository or sender; skipping notification.");
            return;
        }

        // 通知タイトルとキャッシュキーの一意性を保つため、欠損時は空文字ではなく明示的な既定値を使う
        var action = Event.Action ?? "unknown";
        (var title, var color) = GetTitleAndColor(action, issue);

        // サブタイプ固有プロパティをパターンマッチで取得する
        Label? label = (Event as IssuesLabeledEvent)?.Label
                       ?? (Event as IssuesUnlabeledEvent)?.Label;
        User? assignee = (Event as IssuesAssignedEvent)?.Assignee
                         ?? (Event as IssuesUnassignedEvent)?.Assignee;
        Milestone? milestone = (Event as IssuesMilestonedEvent)?.Milestone
                               ?? (Event as IssuesDemilestonedEvent)?.Milestone;
        IssuesEventChanges? changes = (Event as IssuesEditedEvent)?.Changes;

        List<DiscordEmbedField> fields = BuildFields(repo, issue, label, assignee, milestone);
        var description = BuildDescription(action, issue, changes);

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: Uri.TryCreate(issue.HtmlUrl, UriKind.Absolute, out Uri? issueUrl) ? issueUrl : null,
            author: author,
            fields: fields);

        // アクション別キー（同一性質のペアは共通キーで編集対象を統一）
        var keySuffix = GetKeySuffix(action);
        var key = $"{repo.FullName}#{issue.Number}-{keySuffix}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    /// <summary>action ごとの埋め込みタイトルと色を決定する</summary>
    private static (string Title, int Color) GetTitleAndColor(string action, Issue issue) => action switch
    {
        "opened" => ($"Issue opened: #{issue.Number} {issue.Title}", EmbedColors.IssueOpened),
        "closed" => ($"Issue closed: #{issue.Number} {issue.Title}", EmbedColors.IssueClosed),
        "reopened" => ($"Issue reopened: #{issue.Number} {issue.Title}", EmbedColors.IssueReopened),
        "edited" => ($"Issue edited: #{issue.Number} {issue.Title}", EmbedColors.IssueEdited),
        "labeled" => ($"Issue labeled: #{issue.Number} {issue.Title}", EmbedColors.IssueLabeled),
        "unlabeled" => ($"Issue unlabeled: #{issue.Number} {issue.Title}", EmbedColors.IssueUnlabeled),
        "assigned" => ($"Issue assigned: #{issue.Number} {issue.Title}", EmbedColors.IssueAssigned),
        "unassigned" => ($"Issue unassigned: #{issue.Number} {issue.Title}", EmbedColors.IssueUnassigned),
        "milestoned" => ($"Issue milestoned: #{issue.Number} {issue.Title}", EmbedColors.IssueMilestoned),
        "demilestoned" => ($"Issue demilestoned: #{issue.Number} {issue.Title}", EmbedColors.IssueDemilestoned),
        "locked" => ($"Issue locked: #{issue.Number} {issue.Title}", EmbedColors.IssueLocked),
        "unlocked" => ($"Issue unlocked: #{issue.Number} {issue.Title}", EmbedColors.IssueUnlocked),
        "transferred" => ($"Issue transferred: #{issue.Number} {issue.Title}", EmbedColors.IssueTransferred),
        "pinned" => ($"Issue pinned: #{issue.Number} {issue.Title}", EmbedColors.IssuePinned),
        "unpinned" => ($"Issue unpinned: #{issue.Number} {issue.Title}", EmbedColors.IssueUnpinned),
        "deleted" => ($"Issue deleted: #{issue.Number} {issue.Title}", EmbedColors.IssueDeleted),
        _ => ($"Issue {action}: #{issue.Number} {issue.Title}", EmbedColors.Unknown),
    };

    /// <summary>埋め込みフィールド一覧を組み立てる</summary>
    private static List<DiscordEmbedField> BuildFields(
        Repository repo,
        Issue issue,
        Label? label,
        User? assignee,
        Milestone? milestone)
    {
        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("State", issue.State?.StringValue ?? "unknown", true),
        };

        if (label is not null)
            fields.Add(new("Label", label.Name, true));

        if (assignee is not null)
            fields.Add(new("Assignee", assignee.Login, true));

        if (milestone is not null)
            fields.Add(new("Milestone", milestone.Title, true));

        return fields;
    }

    /// <summary>本文（edited イベントの場合はタイトル変更の diff）を組み立てる</summary>
    private static string? BuildDescription(string action, Issue issue, IssuesEventChanges? changes)
    {
        // edited イベントの場合、タイトル変更の diff を description として生成する
        if (action == "edited" && changes?.Title?.From is not null)
        {
            var patch = CreatePatch(changes.Title.From, issue.Title, "title");
            return $"```diff\n{patch}```";
        }

        return issue.Body is not null && issue.Body.Length > 0
            ? (issue.Body.Length > 500 ? $"{issue.Body[..500]}..." : issue.Body)
            : null;
    }

    /// <summary>同一性質のアクションペアを共通キーに統一する</summary>
    private static string GetKeySuffix(string action) => action switch
    {
        "assigned" or "unassigned" => "assigned",
        "labeled" or "unlabeled" => "label",
        "locked" or "unlocked" => "locked",
        "milestoned" or "demilestoned" => "milestoned",
        _ => action,
    };
}
