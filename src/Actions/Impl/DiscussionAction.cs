using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Discussion;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub discussion イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.Discussion)]
public sealed class DiscussionAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<DiscussionAction> logger,
    Uri webhookUrl,
    string eventName,
    DiscussionEvent discussionEvent)
    : BaseAction<DiscussionEvent>(discord, webhookUrl, eventName, discussionEvent, cache, userMapManager, logger)
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

        // サブタイプ固有プロパティをパターンマッチで取得する
        Label? label = (Event as DiscussionLabeledEvent)?.Label
                       ?? (Event as DiscussionUnlabeledEvent)?.Label;
        DiscussionAnswer? answer = (Event as DiscussionAnsweredEvent)?.Answer;
        DiscussionCategory? newCategory = (Event as DiscussionCategoryChangedEvent)?.Changes?.Category?.From;

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
        };

        if (discussion.Category is not null)
            fields.Add(new("Category", discussion.Category.Name, true));

        if (label is not null)
            fields.Add(new("Label", label.Name, true));

        if (newCategory is not null)
            fields.Add(new("New Category", newCategory.Name, true));

        // answered イベント時は回答の本文を表示する
        string? description = null;
        if (Event.Action == "answered" && answer is not null)
        {
            var answerBody = answer.Body ?? string.Empty;
            description = answerBody.Length > 500 ? $"{answerBody[..500]}..." : answerBody;
        }
        else if (!string.IsNullOrEmpty(discussion.Body) && Event.Action is "created" or "edited")
        {
            description = discussion.Body.Length > 500
                ? $"{discussion.Body[..500]}..."
                : discussion.Body;
        }

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: Uri.TryCreate(discussion.HtmlUrl, UriKind.Absolute, out var discussionUrl) ? discussionUrl : null,
            author: author,
            fields: fields);

        var key = $"{repo.FullName}-discussion-{discussion.Number}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }
}
