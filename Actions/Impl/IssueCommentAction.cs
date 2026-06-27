using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub issue_comment イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class IssueCommentAction(IDiscordClient discord, Uri webhookUrl, string eventName, IssueCommentEvent issueCommentEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<IssueCommentEvent>(discord, webhookUrl, eventName, issueCommentEvent, cache, userMapManager, logger)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Issue issue = Event.Issue;
        Comment comment = Event.Comment;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        // Issue に関連するのが PR かどうかで種別を変える
        var issueType = issue.PullRequest is not null ? "PR" : "Issue";

        (var titleVerb, var color) = Event.Action switch
        {
            "created" => ("commented on", EmbedColors.IssueCommentCreated),
            "edited" => ("edited comment on", EmbedColors.IssueCommentEdited),
            "deleted" => ("deleted comment on", EmbedColors.IssueCommentDeleted),
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} {issueType} #{issue.Number}: {issue.Title}";

        var body = comment.Body is not null && comment.Body.Length > 0
            ? (comment.Body.Length > 500 ? $"{comment.Body[..500]}..." : comment.Body)
            : null;

        // Issue 作成者への @mention (送信者自身を除外)
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(issue.User.Id, issue.User.Login)]);

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
            author: author);

        var key = $"{repo.FullName}-issue-comment-{comment.Id}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
