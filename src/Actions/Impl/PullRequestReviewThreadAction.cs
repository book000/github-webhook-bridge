using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>Notifies Discord of GitHub pull_request_review_thread events.</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.PullRequestReviewThread)]
public sealed class PullRequestReviewThreadAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestReviewThreadAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestReviewThreadEvent pullRequestReviewThreadEvent)
    : BaseAction<PullRequestReviewThreadEvent>(discord, webhookUrl, eventName, pullRequestReviewThreadEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        // Event.Review maps to a "review" field that does not exist in the actual payload and is always null,
        // so use AdditionalProperties["thread"].node_id, which holds the real data, as the thread identifier.
        var threadNodeId = GetThreadNodeId();
        SimplePullRequest pr = Event.PullRequest;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("pull_request_review_thread payload is missing repository or sender; skipping notification.");
            return;
        }

        var resolved = Event.Action == "resolved";

        (var titleVerb, var color) = Event.Action switch
        {
            "resolved" => ("resolved review thread on", EmbedColors.PullRequestReviewThreadResolved),
            "unresolved" => ("unresolved review thread on", EmbedColors.PullRequestReviewThreadUnresolved),
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} PR #{pr.Number}: {pr.Title}";

        var fields = new List<DiscordEmbedField>
        {
            new("Thread ID", threadNodeId, false),
            new("Resolved", resolved ? "Yes" : "No", true),
        };

        // @mention the PR author (excluded when the sender is the PR author).
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
            url: Uri.TryCreate(pr.HtmlUrl, UriKind.Absolute, out Uri? prUrl) ? prUrl : null,
            author: author,
            fields: fields);

        // Include the PR number to prevent key collisions when threadNodeId falls back to "unknown".
        var key = $"{repo.FullName}-pr-review-thread-{pr.Number}-{threadNodeId}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }

    /// <summary>
    /// Retrieves "thread.node_id" from AdditionalProperties.
    /// Rather than throwing on an unexpected payload shape, it logs a warning and returns a fallback value.
    /// </summary>
    private string GetThreadNodeId()
    {
        if (Event.AdditionalProperties is { } props
            && props.TryGetValue("thread", out JsonElement thread)
            && thread.ValueKind == JsonValueKind.Object
            && thread.TryGetProperty("node_id", out JsonElement nodeId)
            && nodeId.ValueKind == JsonValueKind.String
            && nodeId.GetString() is { Length: > 0 } value)
        {
            return value;
        }

        Logger.LogWarning("pull_request_review_thread payload is missing thread.node_id; falling back to \"unknown\".");
        return "unknown";
    }
}
