using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub issues イベントを Discord に通知する。</summary>
public sealed class IssuesAction : BaseAction<IssuesEvent>
{
    /// <inheritdoc cref="BaseAction{TEvent}"/>
    public IssuesAction(IDiscordClient d, string wu, string en, IssuesEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var issue  = Event.Issue;
        var repo   = Event.Repository;
        var sender = Event.Sender;

        var (title, color) = Event.Action switch
        {
            "opened"       => ($"Issue opened: #{issue.Number} {issue.Title}", EmbedColors.IssueOpened),
            "closed"       => ($"Issue closed: #{issue.Number} {issue.Title}", EmbedColors.IssueClosed),
            "reopened"     => ($"Issue reopened: #{issue.Number} {issue.Title}", EmbedColors.IssueReopened),
            "edited"       => ($"Issue edited: #{issue.Number} {issue.Title}", EmbedColors.IssueEdited),
            "labeled"      => ($"Issue labeled: #{issue.Number} {issue.Title}", EmbedColors.IssueLabeled),
            "unlabeled"    => ($"Issue unlabeled: #{issue.Number} {issue.Title}", EmbedColors.IssueUnlabeled),
            "assigned"     => ($"Issue assigned: #{issue.Number} {issue.Title}", EmbedColors.IssueAssigned),
            "unassigned"   => ($"Issue unassigned: #{issue.Number} {issue.Title}", EmbedColors.IssueUnassigned),
            "milestoned"   => ($"Issue milestoned: #{issue.Number} {issue.Title}", EmbedColors.IssueMilestoned),
            "demilestoned" => ($"Issue demilestoned: #{issue.Number} {issue.Title}", EmbedColors.IssueDemilestoned),
            "locked"       => ($"Issue locked: #{issue.Number} {issue.Title}", EmbedColors.IssueLocked),
            "unlocked"     => ($"Issue unlocked: #{issue.Number} {issue.Title}", EmbedColors.IssueUnlocked),
            "transferred"  => ($"Issue transferred: #{issue.Number} {issue.Title}", EmbedColors.IssueTransferred),
            "pinned"       => ($"Issue pinned: #{issue.Number} {issue.Title}", EmbedColors.IssuePinned),
            "unpinned"     => ($"Issue unpinned: #{issue.Number} {issue.Title}", EmbedColors.IssueUnpinned),
            "deleted"      => ($"Issue deleted: #{issue.Number} {issue.Title}", EmbedColors.IssueDeleted),
            _              => ($"Issue {Event.Action}: #{issue.Number} {issue.Title}", EmbedColors.Unknown),
        };

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("State",      issue.State,                           true),
        };

        if (Event.Label is not null)
            fields.Add(new("Label", Event.Label.Name, true));

        if (Event.Assignee is not null)
            fields.Add(new("Assignee", Event.Assignee.Login, true));

        if (Event.Milestone is not null)
            fields.Add(new("Milestone", Event.Milestone.Title, true));

        var description = issue.Body is not null && issue.Body.Length > 0
            ? (issue.Body.Length > 500 ? $"{issue.Body[..500]}..." : issue.Body)
            : null;

        // edited イベントの場合、タイトル変更の diff を description として生成する
        if (Event.Action == "edited" && Event.Changes.HasValue)
        {
            var changes = Event.Changes.Value;
            if (changes.TryGetProperty("title", out var titleChange) &&
                titleChange.TryGetProperty("from", out var fromProp))
            {
                var oldTitle = fromProp.GetString() ?? string.Empty;
                var patch    = CreatePatch(oldTitle, issue.Title, "title");
                description  = $"```diff\n{patch}```";
            }
        }

        var author = new DiscordEmbedAuthor(
            Name:    sender.Login,
            Url:     sender.HtmlUrl,
            IconUrl: sender.AvatarUrl);

        var embed = EmbedHelper.CreateEmbed(
            eventName:   EventName,
            color:       color,
            title:       title,
            description: description,
            url:         issue.HtmlUrl,
            author:      author,
            fields:      fields);

        var key = $"{repo.FullName}-issue-{issue.Number}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
