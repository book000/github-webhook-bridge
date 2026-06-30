using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request_review_comment イベントを Discord に通知するクラス</summary>
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
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        (var titleVerb, var color) = Event.Action switch
        {
            "created" => ("commented on", EmbedColors.PullRequestReviewCommentCreated),
            "edited" => ("edited comment on", EmbedColors.PullRequestReviewCommentEdited),
            "deleted" => ("deleted comment on", EmbedColors.PullRequestReviewCommentDeleted),
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} PR #{pr.Number}: {pr.Title}";

        // コメント本文（長い場合は切り詰める）
        var body = comment.Body is not null && comment.Body.Length > 0
            ? (comment.Body.Length > 500 ? $"{comment.Body[..500]}..." : comment.Body)
            : null;

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

        // PR 作成者への @mention
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(pr.User.Id, pr.User.Login)]);
        var content = mentions.Length > 0 ? mentions : null;

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: body,
            url: Uri.TryCreate(comment.HtmlUrl, UriKind.Absolute, out var commentUrl) ? commentUrl : null,
            author: author,
            fields: fields.Count > 0 ? fields : null);

        var key = $"{repo.FullName}-pr-review-comment-{comment.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
