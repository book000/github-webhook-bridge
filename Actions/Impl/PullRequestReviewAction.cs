using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request_review イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PullRequestReviewAction(IDiscordClient d, Uri wu, string en, PullRequestReviewEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : BaseAction<PullRequestReviewEvent>(d, wu, en, e, c, u, l)
{

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        Review review = Event.Review;
        PullRequest pr = Event.PullRequest;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        // レビュー状態とアクションを組み合わせて色とタイトルを決定する
        (var titleVerb, var color) = (Event.Action, review.State.ToUpperInvariant()) switch
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
            Url: sender.HtmlUrl,
            IconUrl: sender.AvatarUrl);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: body,
            url: review.HtmlUrl,
            author: author);

        var key = $"{repo.FullName}-pr-review-{review.Id}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
