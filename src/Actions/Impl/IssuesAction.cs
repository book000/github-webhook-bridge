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

/// <summary>Notifies Discord of GitHub issues events.</summary>
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

        // Use an explicit default rather than an empty string when missing, to keep the notification title and cache key unique.
        var action = Event.Action ?? "unknown";
        (var title, var color) = GetTitleAndColor(action, issue);

        // Retrieve subtype-specific properties via pattern matching.
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

        // Per-action key (pairs of the same nature share a common key to unify the edit target).
        var keySuffix = GetKeySuffix(action);
        var key = $"{repo.FullName}#{issue.Number}-{keySuffix}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    /// <summary>Determines the embed title and color for each action.</summary>
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

    /// <summary>Builds the list of embed fields.</summary>
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

    /// <summary>Builds the body (a title-change diff for the edited event).</summary>
    private static string? BuildDescription(string action, Issue issue, IssuesEventChanges? changes)
    {
        // For the edited event, generate a title-change diff as the description.
        if (action == "edited" && changes?.Title?.From is not null)
        {
            var patch = CreatePatch(changes.Title.From, issue.Title, "title");
            return $"```diff\n{patch}```";
        }

        return issue.Body is not null && issue.Body.Length > 0
            ? (issue.Body.Length > 500 ? $"{issue.Body[..500]}..." : issue.Body)
            : null;
    }

    /// <summary>Unifies pairs of actions of the same nature under a common key.</summary>
    private static string GetKeySuffix(string action) => action switch
    {
        "assigned" or "unassigned" => "assigned",
        "labeled" or "unlabeled" => "label",
        "locked" or "unlocked" => "locked",
        "milestoned" or "demilestoned" => "milestoned",
        _ => action,
    };
}
