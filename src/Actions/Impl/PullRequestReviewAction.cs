using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;
using OctokitReview = Octokit.Webhooks.Models.PullRequestReviewEvent.Review;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request_review イベントを Discord に通知するクラス</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.PullRequestReview)]
public sealed class PullRequestReviewAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestReviewAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestReviewEvent pullRequestReviewEvent)
    : BaseAction<PullRequestReviewEvent>(discord, webhookUrl, eventName, pullRequestReviewEvent, cache, userMapManager, logger)
{
    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        OctokitReview review = Event.Review;
        SimplePullRequest pr = Event.PullRequest;

        if (Event.Repository is not { } repo || Event.Sender is not { } sender)
        {
            Logger.LogWarning("pull_request_review payload is missing repository or sender; skipping notification.");
            return;
        }

        // レビュー状態とアクションを組み合わせて色とタイトルを決定する
        (var titleVerb, var color) = (Event.Action, review.State?.StringValue?.ToUpperInvariant() ?? string.Empty) switch
        {
            ("submitted", "APPROVED") => ("approved", EmbedColors.PullRequestReviewApproved),
            ("submitted", "CHANGES_REQUESTED") => ("requested changes", EmbedColors.PullRequestReviewChangesRequested),
            ("submitted", _) => ("commented on", EmbedColors.PullRequestReviewApproved),
            ("edited", _) => ("edited review on", EmbedColors.PullRequestReviewEdited),
            ("dismissed", _) => ("dismissed review on", EmbedColors.PullRequestReviewDismissed),
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} PR #{pr.Number}: {pr.Title}";

        var body = review.Body is not null && review.Body.Length > 0
            ? (review.Body.Length > 500 ? $"{review.Body[..500]}..." : review.Body)
            : null;

        // PR 作成者への @mention（送信者が自分のレビューをしている場合は除外）
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
            url: Uri.TryCreate(review.HtmlUrl, UriKind.Absolute, out Uri? reviewUrl) ? reviewUrl : null,
            author: author);

        var key = $"{repo.FullName}-pr-review-{review.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
