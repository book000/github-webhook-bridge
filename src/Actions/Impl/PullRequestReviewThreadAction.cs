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

/// <summary>GitHub pull_request_review_thread イベントを Discord に通知するクラス</summary>
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
        // Octokit の PullRequestReviewThreadEvent が持つ Review プロパティは
        // 実際の GitHub ペイロードに存在しない "review" フィールドにマッピングされており常に null になる
        // （実データは "thread" フィールドにある）。そのため AdditionalProperties から
        // thread.node_id を直接読み取り、スレッド識別子として使用する
        var threadNodeId = GetThreadNodeId();
        SimplePullRequest pr = Event.PullRequest;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

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

        // PR 作成者への @mention（送信者が PR 作成者の場合は除外）
        var mentions = await GetUsersMentionsAsync(
            sender.Id,
            [(pr.User.Id, pr.User.Login)]);
        var content = mentions.Length > 0 ? mentions : null;

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            url: Uri.TryCreate(pr.HtmlUrl, UriKind.Absolute, out var prUrl) ? prUrl : null,
            author: author,
            fields: fields);

        var key = $"{repo.FullName}-pr-review-thread-{threadNodeId}";
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }

    /// <summary>
    /// AdditionalProperties から "thread.node_id" を取得する。
    /// 想定外のペイロード形状であっても例外にせず、警告ログを出力して代替値を返す
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
