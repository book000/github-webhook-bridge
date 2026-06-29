using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub issues イベントを Discord に通知する。</summary>
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
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        (var title, var color) = Event.Action switch
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
            _ => ($"Issue {Event.Action}: #{issue.Number} {issue.Title}", EmbedColors.Unknown),
        };

        // サブタイプ固有プロパティをパターンマッチで取得する
        Label? label = (Event as IssuesLabeledEvent)?.Label
                       ?? (Event as IssuesUnlabeledEvent)?.Label;
        User? assignee = (Event as IssuesAssignedEvent)?.Assignee
                         ?? (Event as IssuesUnassignedEvent)?.Assignee;
        Milestone? milestone = (Event as IssuesMilestonedEvent)?.Milestone
                               ?? (Event as IssuesDemilestonedEvent)?.Milestone;
        Octokit.Webhooks.Models.IssuesEvent.Changes? changes = (Event as IssuesEditedEvent)?.Changes;

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("State", issue.State.StringValue, true),
        };

        if (label is not null)
            fields.Add(new("Label", label.Name, true));

        if (assignee is not null)
            fields.Add(new("Assignee", assignee.Login, true));

        if (milestone is not null)
            fields.Add(new("Milestone", milestone.Title, true));

        var description = issue.Body is not null && issue.Body.Length > 0
            ? (issue.Body.Length > 500 ? $"{issue.Body[..500]}..." : issue.Body)
            : null;

        // edited イベントの場合、タイトル変更の diff を description として生成する
        if (Event.Action == "edited" && changes?.Title?.From is not null)
        {
            var oldTitle = changes.Title.From;
            var patch = CreatePatch(oldTitle, issue.Title, "title");
            description = $"```diff\n{patch}```";
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: Uri.TryCreate(issue.HtmlUrl, UriKind.Absolute, out var issueUrl) ? issueUrl : null,
            author: author,
            fields: fields);

        // アクション別キー（同一性質のペアは共通キーで編集対象を統一）
        var keySuffix = Event.Action switch
        {
            "assigned" or "unassigned" => "assigned",
            "labeled" or "unlabeled" => "label",
            "locked" or "unlocked" => "locked",
            "milestoned" or "demilestoned" => "milestoned",
            _ => Event.Action,
        };
        var key = $"{repo.FullName}#{issue.Number}-{keySuffix}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
