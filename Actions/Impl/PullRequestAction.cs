using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub pull_request イベントを Discord に通知する。</summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
public sealed class PullRequestAction(IDiscordClient discord, Uri webhookUrl, string eventName, PullRequestEvent pullRequestEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<PullRequestEvent>(discord, webhookUrl, eventName, pullRequestEvent, cache, userMapManager, logger)
{

    /// <summary>アクションに対応するキャッシュキーのサフィックスを取得します。</summary>
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

    /// <summary>PR とアクションに対応するキャッシュキーを取得します。</summary>
    private string GetCacheKey() =>
        $"{Event.Repository.FullName}#{Event.PullRequest.Number}-{GetCacheKeySuffix()}";

    /// <summary>タイトルが WIP（作業中）かどうかを判定します。</summary>
    private static bool IsWipTitle(string title) =>
        System.Text.RegularExpressions.Regex.IsMatch(title, @"\bwip\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
        title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("wip ", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "PR イベントアクションの種類が多く、switch 式で列挙するため必然的に複雑度が高い")]
    public override async Task RunAsync()
    {
        // synchronize はコミット追加時に発生するが通知不要なためスキップ
        if (Event.Action == "synchronize") return;

        PullRequest pr = Event.PullRequest;
        Repository repo = Event.Repository;
        User sender = Event.Sender;

        (var titleVerb, var color) = Event.Action switch
        {
            "opened" => ("opened", EmbedColors.PullRequestOpened),
            "closed" when pr.Merged == true
                                    => ("merged", EmbedColors.PullRequestMerged),
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
            _ => (Event.Action, EmbedColors.Unknown),
        };

        var title = $"PR {titleVerb}: #{pr.Number} {pr.Title}";

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", $"[{repo.FullName}]({repo.HtmlUrl})", true),
            new("Branch", $"`{pr.Head.Ref}` → `{pr.Base.Ref}`", true),
        };

        if (pr.Additions.HasValue && pr.Deletions.HasValue)
            fields.Add(new("Changes", $"+{pr.Additions} / -{pr.Deletions} ({pr.ChangedFiles} files)", true));

        if (pr.Draft)
            fields.Add(new("Status", "Draft", true));

        if (Event.Label is not null)
            fields.Add(new("Label", Event.Label.Name, true));

        if (Event.Assignee is not null)
            fields.Add(new("Assignee", Event.Assignee.Login, true));

        if (Event.RequestedReviewer is not null)
            fields.Add(new("Requested Reviewer", Event.RequestedReviewer.Login, true));

        // PR 作成者への @mention が必要なアクションで通知する
        // Draft PR はまだレビュー準備ができていないためメンションを抑制する
        string? content = null;
        if ((Event.Action is "review_requested" or "assigned") && !pr.Draft)
        {
            var targets = new List<(long, string)>();

            if (Event.RequestedReviewer is not null)
                targets.Add((Event.RequestedReviewer.Id, Event.RequestedReviewer.Login));

            if (Event.Assignee is not null)
                targets.Add((Event.Assignee.Id, Event.Assignee.Login));

            var mentions = await GetUsersMentionsAsync(sender.Id, targets);
            if (mentions.Length > 0) content = mentions;
        }

        // PR 本文（長い場合は切り詰める）
        string? description = null;
        if (Event.Action is "opened" or "edited")
        {
            if (!string.IsNullOrEmpty(pr.Body))
                description = pr.Body.Length > 500 ? $"{pr.Body[..500]}..." : pr.Body;

            // edited の場合、タイトル変更の diff を生成する
            if (Event.Action == "edited" && Event.Changes.HasValue)
            {
                JsonElement changes = Event.Changes.Value;
                if (changes.TryGetProperty("title", out JsonElement titleChange) &&
                    titleChange.TryGetProperty("from", out JsonElement fromProp))
                {
                    var oldTitle = fromProp.GetString() ?? string.Empty;
                    var patch = CreatePatch(oldTitle, pr.Title, "title");
                    description = $"```diff\n{patch}```";
                }
            }
        }

        // edited の場合、WIP タイトルが解除されたらレビュアーへメンション
        // Draft PR はまだレビュー準備ができていないためメンションを抑制する
        if (Event.Action == "edited" && Event.Changes.HasValue && !pr.Draft)
        {
            JsonElement changes = Event.Changes.Value;
            if (changes.TryGetProperty("title", out JsonElement titleChangeProp) &&
                titleChangeProp.TryGetProperty("from", out JsonElement previousTitleProp))
            {
                var previousTitle = previousTitleProp.GetString();
                if (previousTitle is not null &&
                    IsWipTitle(previousTitle) && !IsWipTitle(pr.Title))
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
            url: pr.HtmlUrl,
            author: author,
            fields: fields);

        var key = GetCacheKey();
        await SendMessageAsync(key, new DiscordMessage(Content: content, Embeds: [embed]));
    }
}
