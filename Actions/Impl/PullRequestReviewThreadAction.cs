using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request_review_thread イベントを Discord に通知する。</summary>
public sealed class PullRequestReviewThreadAction : BaseAction<PullRequestReviewThreadEvent>
{
    /// <inheritdoc cref="BaseAction{TEvent}"/>
    public PullRequestReviewThreadAction(IDiscordClient d, string wu, string en, PullRequestReviewThreadEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var thread = Event.Thread;
        var pr     = Event.PullRequest;
        var repo   = Event.Repository;
        var sender = Event.Sender;

        var (titleVerb, color) = Event.Action switch
        {
            "resolved"   => ("resolved review thread on",   EmbedColors.PullRequestReviewThreadResolved),
            "unresolved" => ("unresolved review thread on", EmbedColors.PullRequestReviewThreadUnresolved),
            _            => (Event.Action,                  EmbedColors.Unknown),
        };

        var title = $"{sender.Login} {titleVerb} PR #{pr.Number}: {pr.Title}";

        var fields = new List<DiscordEmbedField>
        {
            new("Thread ID", thread.NodeId, false),
            new("Resolved",  thread.Resolved ? "Yes" : "No", true),
        };

        // PR 作成者への @mention（送信者が PR 作成者の場合は除外）
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(pr.User.Id, pr.User.Login)]);
        var content = mentions.Length > 0 ? mentions : null;

        var author = new DiscordEmbedAuthor(
            Name:    sender.Login,
            Url:     sender.HtmlUrl,
            IconUrl: sender.AvatarUrl);

        var embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color:     color,
            title:     title,
            url:       pr.HtmlUrl,
            author:    author,
            fields:    fields);

        var key = $"{repo.FullName}-pr-review-thread-{thread.NodeId}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
