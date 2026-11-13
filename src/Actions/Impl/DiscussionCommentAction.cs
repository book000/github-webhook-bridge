using System.Globalization;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;
using DiscussionCommentModel = Octokit.Webhooks.Models.DiscussionCommentEvent.DiscussionComment;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub discussion_comment events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.DiscussionComment)]
public sealed class DiscussionCommentAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<DiscussionCommentAction> logger,
    Uri webhookUrl,
    string eventName,
    DiscussionCommentEvent discussionCommentEvent)
    : BaseAction<DiscussionCommentEvent>(discord, webhookUrl, eventName, discussionCommentEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Discussion discussion = Event.Discussion;
        DiscussionCommentModel comment = Event.Comment;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("discussion_comment payload is missing repository or sender; skipping notification.");
            return;
        }

        var action = Event.Action ?? "unknown";

        (var titleVerb, var color) = action switch
        {
            "created" => ("commented on", EmbedColors.DiscussionCommentCreated),
            "edited" => ("edited comment on", EmbedColors.DiscussionCommentEdited),
            "deleted" => ("deleted comment on", EmbedColors.DiscussionCommentDeleted),
            _ => (action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} Discussion #{discussion.Number}: {discussion.Title}";

        // Octokit.Webhooks models Changes.Body as Changes itself, so the previous body is unreachable and no diff is shown for edits.
        var body = comment.Body is { Length: > 0 }
            ? (comment.Body.Length > 500 ? $"{comment.Body[..500]}..." : comment.Body)
            : null;

        var mentions = SuppressesMentions(action)
            ? string.Empty
            : await GetUsersMentionsAsync(
                sender.Id,
                [(discussion.User.Id, discussion.User.Login)]);

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

        var key = $"{repo.FullName}-discussion-comment-{comment.Id.ToString(CultureInfo.InvariantCulture)}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
