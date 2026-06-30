using System.Text.RegularExpressions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequest;
using Octokit.Webhooks.Models;
using OctokitPR = Octokit.Webhooks.Models.PullRequestEvent.PullRequest;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request イベントを Discord に通知するクラス</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.PullRequest)]
public sealed class PullRequestAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestEvent pullRequestEvent)
    : BaseAction<PullRequestEvent>(discord, webhookUrl, eventName, pullRequestEvent, cache, userMapManager, logger)
{
    /// <summary>アクションに対応するキャッシュキーのサフィックスを取得する</summary>
    private string GetCacheKeySuffix() => Event.Action switch
    {
        "assigned" or "unassigned" => "assigned",
        "labeled" or "unlabeled" => "label",
        "locked" or "unlocked" => "locked",
        "auto_merge_enabled" or "auto_merge_disabled" => "auto_merge_enabled",
        "milestoned" or "demilestoned" => "milestoned",
        "review_requested" or "review_request_removed" => "review_requested",
        "enqueued" or "dequeued" => "enqueued",
        _ => Event.Action,
    };

    /// <summary>PR とアクションに対応するキャッシュキーを取得する</summary>
    private string GetCacheKey() =>
        $"{Event.Repository.FullName}#{Event.PullRequest.Number}-{GetCacheKeySuffix()}";

    /// <summary>タイトルが WIP（作業中）かどうかを判定する</summary>
    private static bool IsWipTitle(string title) =>
        Regex.IsMatch(title, @"\bwip\b", RegexOptions.IgnoreCase) ||
        title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("wip ", StringComparison.OrdinalIgnoreCase);

    /// <summary>PR アクション名をタイトル動詞と Embed カラーにマッピングする</summary>
    /// <param name="action">GitHub Webhook の pull_request.action 値</param>
    /// <param name="merged">PR がマージ済みかどうか（"closed" アクション時に参照）</param>
    /// <returns>タイトル動詞と Discord Embed カラーのタプル</returns>
    private static (string TitleVerb, int Color) GetTitleVerbAndColor(string action, bool merged)
        => action switch
        {
            "opened" => ("opened", EmbedColors.PullRequestOpened),
            "closed" when merged => ("merged", EmbedColors.PullRequestMerged),
            "closed" => ("closed", EmbedColors.PullRequestClosed),
            "reopened" => ("reopened", EmbedColors.PullRequestReopened),
            "assigned" => ("assigned", EmbedColors.PullRequestAssigned),
            "unassigned" => ("unassigned", EmbedColors.PullRequestUnassigned),
            "review_requested" => ("review requested", EmbedColors.PullRequestReviewRequested),
            "review_request_removed" => ("review request removed", EmbedColors.PullRequestReviewRequestRemoved),
            "labeled" => ("labeled", EmbedColors.PullRequestLabeled),
            "unlabeled" => ("unlabeled", EmbedColors.PullRequestUnlabeled),
            "edited" => ("edited", EmbedColors.PullRequestEdited),
            "ready_for_review" => ("ready for review", EmbedColors.PullRequestReadyForReview),
            "converted_to_draft" => ("converted to draft", EmbedColors.PullRequestConvertedToDraft),
            "locked" => ("locked", EmbedColors.PullRequestLocked),
            "unlocked" => ("unlocked", EmbedColors.PullRequestUnlocked),
            "auto_merge_enabled" => ("auto merge enabled", EmbedColors.PullRequestAutoMergeEnabled),
            "auto_merge_disabled" => ("auto merge disabled", EmbedColors.PullRequestAutoMergeDisabled),
            "milestoned" => ("milestoned", EmbedColors.PullRequestMilestoned),
            "demilestoned" => ("demilestoned", EmbedColors.PullRequestDemilestoned),
            "enqueued" => ("enqueued", EmbedColors.PullRequestEnqueued),
            "dequeued" => ("dequeued", EmbedColors.PullRequestDequeued),
            _ => (action, EmbedColors.Unknown),
        };

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        // synchronize はコミット追加時に発生するが通知不要なためスキップ
        if (Event.Action == "synchronize") return;

        OctokitPR pr = Event.PullRequest;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        // サブタイプ固有プロパティをパターンマッチで取得する
        Label? label = (Event as PullRequestLabeledEvent)?.Label
                       ?? (Event as PullRequestUnlabeledEvent)?.Label;
        User? assignee = (Event as PullRequestAssignedEvent)?.Assignee
                         ?? (Event as PullRequestUnassignedEvent)?.Assignee;
        User? requestedReviewer = (Event as PullRequestReviewRequestedEvent)?.RequestedReviewer;
        Octokit.Webhooks.Models.PullRequestEvent.PullRequestEditedEventChanges? changes =
            (Event as PullRequestEditedEvent)?.Changes;

        (var titleVerb, var color) = GetTitleVerbAndColor(Event.Action, pr.Merged == true);

        var title = $"PR {titleVerb}: #{pr.Number} {pr.Title}";
        List<DiscordEmbedField> fields = BuildFields(pr, repo, label, assignee, requestedReviewer);
        var content = await BuildContentAsync(pr, sender, assignee, requestedReviewer, changes);
        var description = BuildDescription(pr, changes);

        var author = new DiscordEmbedAuthor(
            Name: sender.Login,
            Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out var senderUrl) ? senderUrl : null,
            IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out var avatarUrl) ? avatarUrl : null);

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            description: description,
            url: Uri.TryCreate(pr.HtmlUrl, UriKind.Absolute, out var prUrl) ? prUrl : null,
            author: author,
            fields: fields);

        var key = GetCacheKey();
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }

    /// <summary>PR の各種情報を Embed フィールドのリストとして構築する</summary>
    private static List<DiscordEmbedField> BuildFields(
        OctokitPR pr, Repository repo,
        Label? label, User? assignee, User? requestedReviewer)
    {
        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("Branch", $"`{pr.Head.Ref}` → `{pr.Base.Ref}`", true),
        };

        // Octokit の PullRequest では Additions/Deletions は long（常に存在）
        fields.Add(new("Changes", $"+{pr.Additions} / -{pr.Deletions} ({pr.ChangedFiles} files)", true));

        if (pr.Draft)
            fields.Add(new("Status", "Draft", true));

        if (label is not null)
            fields.Add(new("Label", label.Name, true));

        if (assignee is not null)
            fields.Add(new("Assignee", assignee.Login, true));

        if (requestedReviewer is not null)
            fields.Add(new("Requested Reviewer", requestedReviewer.Login, true));

        return fields;
    }

    /// <summary>
    /// アクションに応じてメンション文字列を構築する。
    /// Draft PR および WIP タイトル解除前はメンションを抑制する
    /// </summary>
    private async Task<string?> BuildContentAsync(
        OctokitPR pr, User sender,
        User? assignee, User? requestedReviewer,
        Octokit.Webhooks.Models.PullRequestEvent.PullRequestEditedEventChanges? changes)
    {
        string? content = null;

        // review_requested / assigned 時にレビュアー・アサイニーへメンション
        // Draft PR はまだレビュー準備ができていないためメンションを抑制する
        if ((Event.Action is "review_requested" or "assigned") && !pr.Draft)
        {
            var targets = new List<(long, string)>();

            if (requestedReviewer is not null)
                targets.Add((requestedReviewer.Id, requestedReviewer.Login));

            if (assignee is not null)
                targets.Add((assignee.Id, assignee.Login));

            var mentions = await GetUsersMentionsAsync(sender.Id, targets);
            if (mentions.Length > 0) content = mentions;
        }

        // edited の場合、WIP タイトルが解除されたらレビュアーへメンション
        // Draft PR はまだレビュー準備ができていないためメンションを抑制する
        if (Event.Action == "edited" && changes?.Title?.From is not null && !pr.Draft)
        {
            var previousTitle = changes.Title.From;
            if (IsWipTitle(previousTitle) && !IsWipTitle(pr.Title))
            {
                IEnumerable<(long Id, string Login)> reviewers = (pr.RequestedReviewers ?? [])
                    .Select(u => (u.Id, u.Login));
                var wipMentions = await GetUsersMentionsAsync(sender.Id, reviewers);
                if (wipMentions.Length > 0)
                {
                    content = string.IsNullOrEmpty(content)
                        ? wipMentions
                        : $"{content} {wipMentions}";
                }
            }
        }

        return content;
    }

    /// <summary>
    /// opened / edited アクション時に Embed の説明文を構築する。
    /// edited かつタイトル変更がある場合は diff 形式で表示する
    /// </summary>
    private string? BuildDescription(
        OctokitPR pr,
        Octokit.Webhooks.Models.PullRequestEvent.PullRequestEditedEventChanges? changes)
    {
        if (Event.Action is not ("opened" or "edited"))
            return null;

        // edited の場合、タイトル変更の diff を優先して表示する
        if (Event.Action == "edited" && changes?.Title?.From is not null)
        {
            var oldTitle = changes.Title.From;
            var patch = CreatePatch(oldTitle, pr.Title, "title");
            return $"```diff\n{patch}```";
        }

        // PR 本文（長い場合は切り詰める）
        if (!string.IsNullOrEmpty(pr.Body))
            return pr.Body.Length > 500 ? $"{pr.Body[..500]}..." : pr.Body;

        return null;
    }
}
