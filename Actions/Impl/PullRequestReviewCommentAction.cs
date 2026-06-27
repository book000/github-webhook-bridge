using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request_review_comment イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PullRequestReviewCommentAction(IDiscordClient discord, Uri webhookUrl, string eventName, PullRequestReviewCommentEvent pullRequestReviewCommentEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<PullRequestReviewCommentEvent>(discord, webhookUrl, eventName, pullRequestReviewCommentEvent, cache, userMapManager, logger)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        ReviewComment comment = Event.Comment;
        PullRequest pr = Event.PullRequest;
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
            Url: sender.HtmlUrl,
            IconUrl: sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: body,
            url: comment.HtmlUrl,
            author: author,
            fields: fields.Count > 0 ? fields : null);

        var key = $"{repo.FullName}-pr-review-comment-{comment.Id}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
