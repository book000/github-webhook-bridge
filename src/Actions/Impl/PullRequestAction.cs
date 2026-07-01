using System.Text.RegularExpressions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequest;
using Octokit.Webhooks.Models;
using OctokitPR = Octokit.Webhooks.Models.PullRequestEvent.PullRequest;
using PullRequestEditedEventChanges = Octokit.Webhooks.Models.PullRequestEvent.PullRequestEditedEventChanges;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub pull_request events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.PullRequest)]
public sealed class PullRequestAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestEvent pullRequestEvent)
    : BaseAction<PullRequestEvent>(discord, webhookUrl, eventName, pullRequestEvent, cache, userMapManager, logger)
{
    /// <summary>Gets the cache key suffix corresponding to the action.</summary>
    private static string GetCacheKeySuffix(string action) => action switch
    {
        "assigned" or "unassigned" => "assigned",
        "labeled" or "unlabeled" => "label",
        "locked" or "unlocked" => "locked",
        "auto_merge_enabled" or "auto_merge_disabled" => "auto_merge_enabled",
        "milestoned" or "demilestoned" => "milestoned",
        "review_requested" or "review_request_removed" => "review_requested",
        "enqueued" or "dequeued" => "enqueued",
        _ => action,
    };

    /// <summary>Gets the cache key corresponding to the PR and action.</summary>
    private static string GetCacheKey(Repository repo, OctokitPR pr, string action) =>
        $"{repo.FullName}#{pr.Number}-{GetCacheKeySuffix(action)}";

    /// <summary>Determines whether the title indicates WIP (work in progress).</summary>
    private static bool IsWipTitle(string title) =>
        Regex.IsMatch(title, @"\bwip\b", RegexOptions.IgnoreCase) ||
        title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("wip ", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a PR action name to a title verb and an embed color.</summary>
    /// <param name="action">The GitHub Webhook pull_request.action value.</param>
    /// <param name="merged">Whether the PR is merged (referenced on the "closed" action).</param>
    /// <returns>A tuple of the title verb and the Discord embed color.</returns>
    private static (string TitleVerb, int Color) GetTitleVerbAndColor(string action, bool merged)
        => action switch
        {
            "opened" => ("opened", EmbedColors.PullRequestOpened),
            "closed" when merged => ("merged", EmbedColors.PullRequestMerged),
            "closed" => ("closed", EmbedColors.PullRequestClosed),
            "reopened" => ("reopened", EmbedColors.PullRequestReopened),
            "assigned" => ("assigned", EmbedColors.PullRequestAssigned),
            "unassigned" => ("unassigned", EmbedColors.PullRequestUnassigned),
            "review_requested" => ("review requested", EmbedColors.PullRequestReviewRequested),
            "review_request_removed" => ("review request removed", EmbedColors.PullRequestReviewRequestRemoved),
            "labeled" => ("labeled", EmbedColors.PullRequestLabeled),
            "unlabeled" => ("unlabeled", EmbedColors.PullRequestUnlabeled),
            "edited" => ("edited", EmbedColors.PullRequestEdited),
            "ready_for_review" => ("ready for review", EmbedColors.PullRequestReadyForReview),
            "converted_to_draft" => ("converted to draft", EmbedColors.PullRequestConvertedToDraft),
            "locked" => ("locked", EmbedColors.PullRequestLocked),
            "unlocked" => ("unlocked", EmbedColors.PullRequestUnlocked),
            "auto_merge_enabled" => ("auto merge enabled", EmbedColors.PullRequestAutoMergeEnabled),
            "auto_merge_disabled" => ("auto merge disabled", EmbedColors.PullRequestAutoMergeDisabled),
            "milestoned" => ("milestoned", EmbedColors.PullRequestMilestoned),
            "demilestoned" => ("demilestoned", EmbedColors.PullRequestDemilestoned),
            "enqueued" => ("enqueued", EmbedColors.PullRequestEnqueued),
            "dequeued" => ("dequeued", EmbedColors.PullRequestDequeued),
            _ => (action, EmbedColors.Unknown),
        };

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        // synchronize fires when commits are added, but no notification is needed, so skip it.
        if (Event.Action == "synchronize") return;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("pull_request payload is missing repository or sender; skipping notification.");
            return;
        }

        OctokitPR pr = Event.PullRequest;
        // Use an explicit default rather than an empty string when missing, to keep the notification title and cache key unique.
        var action = Event.Action ?? "unknown";

        // Retrieve subtype-specific properties via pattern matching.
        Label? label = (Event as PullRequestLabeledEvent)?.Label
                       ?? (Event as PullRequestUnlabeledEvent)?.Label;
        User? assignee = (Event as PullRequestAssignedEvent)?.Assignee
                         ?? (Event as PullRequestUnassignedEvent)?.Assignee;
        User? requestedReviewer = (Event as PullRequestReviewRequestedEvent)?.RequestedReviewer;
        PullRequestEditedEventChanges? changes =
            (Event as PullRequestEditedEvent)?.Changes;

        (var titleVerb, var color) = GetTitleVerbAndColor(action, pr.Merged == true);

        var title = $"PR {titleVerb}: #{pr.Number} {pr.Title}";
        List<DiscordEmbedField> fields = BuildFields(pr, repo, label, assignee, requestedReviewer);
        var content = await BuildContentAsync(pr, sender, assignee, requestedReviewer, changes);
        var description = BuildDescription(pr, changes);

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: Uri.TryCreate(pr.HtmlUrl, UriKind.Absolute, out Uri? prUrl) ? prUrl : null,
            author: author,
            fields: fields);

        var key = GetCacheKey(repo, pr, action);
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }

    /// <summary>Builds the various PR details into a list of embed fields.</summary>
    private static List<DiscordEmbedField> BuildFields(
        OctokitPR pr,
        Repository repo,
        Label? label,
        User? assignee,
        User? requestedReviewer)
    {
        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("Branch", $"`{pr.Head.Ref}` → `{pr.Base.Ref}`", true),
            // In Octokit's PullRequest, Additions/Deletions are long (always present).
            new("Changes", $"+{pr.Additions} / -{pr.Deletions} ({pr.ChangedFiles} files)", true),
        };

        if (pr.Draft)
            fields.Add(new("Status", "Draft", true));

        if (label is not null)
            fields.Add(new("Label", label.Name, true));

        if (assignee is not null)
            fields.Add(new("Assignee", assignee.Login, true));

        if (requestedReviewer is not null)
            fields.Add(new("Requested Reviewer", requestedReviewer.Login, true));

        return fields;
    }

    /// <summary>
    /// Builds the mention string based on the action.
    /// Mentions are suppressed for draft PRs and before the WIP title is cleared.
    /// </summary>
    private async Task<string?> BuildContentAsync(
        OctokitPR pr,
        User sender,
        User? assignee,
        User? requestedReviewer,
        PullRequestEditedEventChanges? changes)
    {
        string? content = null;

        // On review_requested / assigned, mention the reviewer and assignee.
        // Suppress mentions for draft PRs since they are not yet ready for review.
        if ((Event.Action is "review_requested" or "assigned") && !pr.Draft)
        {
            var targets = new List<(long, string)>();

            if (requestedReviewer is not null)
                targets.Add((requestedReviewer.Id, requestedReviewer.Login));

            if (assignee is not null)
                targets.Add((assignee.Id, assignee.Login));

            var mentions = await GetUsersMentionsAsync(sender.Id, targets);
            if (mentions.Length > 0) content = mentions;
        }

        // On edited, mention reviewers once the WIP title is cleared.
        // Suppress mentions for draft PRs since they are not yet ready for review.
        if (Event.Action == "edited" && changes?.Title?.From is not null && !pr.Draft)
        {
            var previousTitle = changes.Title.From;
            if (IsWipTitle(previousTitle) && !IsWipTitle(pr.Title))
            {
                IEnumerable<(long Id, string Login)> reviewers = (pr.RequestedReviewers ?? [])
                    .Select(u => (u.Id, u.Login));
                var wipMentions = await GetUsersMentionsAsync(sender.Id, reviewers);
                if (wipMentions.Length > 0)
                {
                    content = string.IsNullOrEmpty(content)
                        ? wipMentions
                        : $"{content} {wipMentions}";
                }
            }
        }

        return content;
    }

    /// <summary>
    /// Builds the embed description on the opened / edited actions.
    /// If edited and the title changed, it is shown in diff format.
    /// </summary>
    private string? BuildDescription(
        OctokitPR pr,
        PullRequestEditedEventChanges? changes)
    {
        if (Event.Action is not ("opened" or "edited"))
            return null;

        // On edited, prefer showing the title-change diff.
        if (Event.Action == "edited" && changes?.Title?.From is not null)
        {
            var oldTitle = changes.Title.From;
            var patch = CreatePatch(oldTitle, pr.Title, "title");
            return $"```diff\n{patch}```";
        }

        // PR body (truncated if long).
        if (!string.IsNullOrEmpty(pr.Body))
            return pr.Body.Length > 500 ? $"{pr.Body[..500]}..." : pr.Body;

        return null;
    }
}
