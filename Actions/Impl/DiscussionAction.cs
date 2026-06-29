using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub discussion イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class DiscussionAction(IDiscordClient discord, Uri webhookUrl, string eventName, DiscussionEvent discussionEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<DiscussionEvent>(discord, webhookUrl, eventName, discussionEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Discussion discussion = Event.Discussion;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        (var titleVerb, var color) = Event.Action switch
        {
            "created" => ("created", EmbedColors.DiscussionCreated),
            "edited" => ("edited", EmbedColors.DiscussionEdited),
            "deleted" => ("deleted", EmbedColors.DiscussionDeleted),
            "pinned" => ("pinned", EmbedColors.DiscussionPinned),
            "unpinned" => ("unpinned", EmbedColors.DiscussionUnpinned),
            "locked" => ("locked", EmbedColors.DiscussionLocked),
            "unlocked" => ("unlocked", EmbedColors.DiscussionUnlocked),
            "transferred" => ("transferred", EmbedColors.DiscussionTransferred),
            "answered" => ("answered", EmbedColors.DiscussionAnswered),
            "unanswered" => ("unanswered", EmbedColors.DiscussionUnanswered),
            "labeled" => ("labeled", EmbedColors.DiscussionLabeled),
            "unlabeled" => ("unlabeled", EmbedColors.DiscussionUnlabeled),
            "category_changed" => ("changed category", EmbedColors.DiscussionCategoryChanged),
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"Discussion {titleVerb}: #{discussion.Number} {discussion.Title}";

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
        };

        if (discussion.Category is not null)
            fields.Add(new("Category", discussion.Category.Name, true));

        if (Event.Label is not null)
            fields.Add(new("Label", Event.Label.Name, true));

        if (Event.Category is not null)
            fields.Add(new("New Category", Event.Category.Name, true));

        // answered イベント時は回答コメントの本文を表示する
        string? description = null;
        if (Event.Action == "answered" && Event.Comment is not null)
        {
            var commentBody = Event.Comment.Body ?? string.Empty;
            description = commentBody.Length > 500 ? $"{commentBody[..500]}..." : commentBody;
        }
        else if (!string.IsNullOrEmpty(discussion.Body) && Event.Action is "created" or "edited")
        {
            description = discussion.Body.Length > 500
                ? $"{discussion.Body[..500]}..."
                : discussion.Body;
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: sender.HtmlUrl,
            IconUrl: sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: discussion.HtmlUrl,
            author: author,
            fields: fields);

        var key = $"{repo.FullName}-discussion-{discussion.Number}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
