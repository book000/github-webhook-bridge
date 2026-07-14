using System.Globalization;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequestReviewComment;
using Octokit.Webhooks.Models;
using PullRequestReviewCommentEventChanges = Octokit.Webhooks.Models.PullRequestReviewCommentEvent.Changes;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub pull_request_review_comment events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.PullRequestReviewComment)]
public sealed class PullRequestReviewCommentAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestReviewCommentAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestReviewCommentEvent pullRequestReviewCommentEvent)
    : BaseAction<PullRequestReviewCommentEvent>(discord, webhookUrl, eventName, pullRequestReviewCommentEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        PullRequestReviewComment comment = Event.Comment;
        SimplePullRequest pr = Event.PullRequest;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("pull_request_review_comment payload is missing repository or sender; skipping notification.");
            return;
        }

        // Use an explicit default rather than an empty string when missing, to keep the notification title unique.
        var action = Event.Action ?? "unknown";

        (var titleVerb, var color) = action switch
        {
            "created" => ("commented on", EmbedColors.PullRequestReviewCommentCreated),
            "edited" => ("edited comment on", EmbedColors.PullRequestReviewCommentEdited),
            "deleted" => ("deleted comment on", EmbedColors.PullRequestReviewCommentDeleted),
            _ => (action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} PR #{pr.Number}: {pr.Title}";

        PullRequestReviewCommentEventChanges? changes = (Event as PullRequestReviewCommentEditedEvent)?.Changes;
        var body = BuildDescription(action, comment, changes);

        var fields = new List<DiscordEmbedField>();
        if (comment.Path is not null)
            fields.Add(new("File", comment.Path, true));

        if (comment.DiffHunk is not null)
        {
            var hunk = comment.DiffHunk.Length > 300
                ? $"{comment.DiffHunk[..300]}..."
                : comment.DiffHunk;
            fields.Add(new("Diff", $"```diff\n{hunk}```", false));
        }

        // @mention the PR author.
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(pr.User.Id, pr.User.Login)]);
        var content = mentions.Length > 0 ? mentions : null;

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: body,
            url: Uri.TryCreate(comment.HtmlUrl, UriKind.Absolute, out Uri? commentUrl) ? commentUrl : null,
            author: author,
            fields: fields.Count > 0 ? fields : null);

        var key = $"{repo.FullName}-pr-review-comment-{comment.Id.ToString(CultureInfo.InvariantCulture)}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }

    /// <summary>Builds the description (a body-change diff for edited events).</summary>
    private static string? BuildDescription(
        string action,
        PullRequestReviewComment comment,
        PullRequestReviewCommentEventChanges? changes)
    {
        // For edited events, render the body change as a diff description.
        if (action == "edited" && changes?.Body?.From is not null)
        {
            var patch = CreatePatch(changes.Body.From, comment.Body ?? string.Empty, "comment");
            return BuildDiffDescription(patch);
        }

        return comment.Body is not null && comment.Body.Length > 0
            ? (comment.Body.Length > 500 ? $"{comment.Body[..500]}..." : comment.Body)
            : null;
    }
}
