using System.Globalization;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub issue_comment events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.IssueComment)]
public sealed class IssueCommentAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<IssueCommentAction> logger,
    Uri webhookUrl,
    string eventName,
    IssueCommentEvent issueCommentEvent)
    : BaseAction<IssueCommentEvent>(discord, webhookUrl, eventName, issueCommentEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Issue issue = Event.Issue;
        IssueComment comment = Event.Comment;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("issue_comment payload is missing repository or sender; skipping notification.");
            return;
        }

        // Vary the type depending on whether the issue is a PR (it is a PR if IssuePullRequest.HtmlUrl is non-empty).
        var issueType = !string.IsNullOrEmpty(issue.PullRequest?.HtmlUrl) ? "PR" : "Issue";

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

        // @mention the issue author (excluding the sender).
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(issue.User.Id, issue.User.Login)]);

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
            author: author);

        var key = $"{repo.FullName}-issue-comment-{comment.Id.ToString(CultureInfo.InvariantCulture)}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
