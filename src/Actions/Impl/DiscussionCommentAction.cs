using System.Globalization;
using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

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
    DiscussionCommentEvent discussionCommentEvent,
    RawPayload? rawPayload = null)
    : BaseAction<DiscussionCommentEvent>(discord, webhookUrl, eventName, discussionCommentEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("discussion_comment payload is missing repository or sender; skipping notification.");
            return;
        }

        if (Event.Discussion is not { } discussion || Event.Comment is not { } comment)
        {
            Logger.LogWarning("discussion_comment payload is missing discussion or comment; skipping notification.");
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

        string? body;
        if (action == "edited" && GetPreviousBody() is { } previousBody)
        {
            body = BuildDiffDescription(CreatePatch(previousBody, comment.Body ?? string.Empty, "comment"));
        }
        else
        {
            if (action == "edited")
                Logger.LogWarning("discussion_comment edited payload is missing changes.body.from; falling back to the current body.");

            body = comment.Body is { Length: > 0 }
                ? TextHelper.Truncate(comment.Body, 500)
                : null;
        }

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

    /// <summary>
    /// Reads "changes.body.from" from the raw payload, because Octokit.Webhooks drops it from the typed "changes" model.
    /// Returns null for any other shape; an empty string is a valid previous body.
    /// </summary>
    private string? GetPreviousBody()
    {
        if (rawPayload is null)
            return null;

        using var doc = JsonDocument.Parse(rawPayload.Json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("changes", out JsonElement changes)
            && changes.ValueKind == JsonValueKind.Object
            && changes.TryGetProperty("body", out JsonElement bodyChange)
            && bodyChange.ValueKind == JsonValueKind.Object
            && bodyChange.TryGetProperty("from", out JsonElement previous)
            && previous.ValueKind == JsonValueKind.String)
        {
            return previous.GetString();
        }

        return null;
    }
}
