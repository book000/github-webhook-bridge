using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Tests;

/// <summary>PullRequestReviewAction の色・タイトル検証テスト。</summary>
public class PullRequestReviewActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.com/api/webhooks/1/x");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    /// <summary>テスト用 PullRequestReviewEvent を TestFixtures から生成する。</summary>
    private static PullRequestReviewEvent MakePrReviewEvent(string action, string reviewState) =>
        JsonSerializer.Deserialize<PullRequestReviewEvent>(
            $$"""
            {
                "action":"{{action}}",
                "review":{{TestFixtures.ReviewJson(1, reviewState.ToLowerInvariant(),
                    "https://github.com/owner/repo/pull/1#pullrequestreview-1")}},
                "pull_request":{{TestFixtures.SimplePrJson(
                    1, "Test PR",
                    "https://github.com/owner/repo/pull/1",
                    "pr-author", 2)}},
                "repository":{{TestFixtures.RepoJson("owner/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",1)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>キャプチャされた Embed の色を取得するヘルパー。</summary>
    private static async Task<int> RunAndCaptureColor(
        Mock<IDiscordClient> discord,
        Mock<IMessageCacheService> cache,
        Mock<IGitHubUserMapManager> userMap,
        string action,
        string state)
    {
        PullRequestReviewEvent prEvent = MakePrReviewEvent(action, state);

        PullRequestReviewAction reviewAction = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        var capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1;
               })
               .ReturnsAsync("msg-id");

        await reviewAction.RunAsync();
        return capturedColor;
    }

    /// <summary>submitted + APPROVED のレビューは PullRequestReviewApproved 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncSubmittedApprovedUsesApprovedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "APPROVED");

        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>submitted + CHANGES_REQUESTED は PullRequestReviewChangesRequested 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncSubmittedChangesRequestedUsesChangesRequestedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "CHANGES_REQUESTED");

        Assert.Equal(EmbedColors.PullRequestReviewChangesRequested, color);
    }

    /// <summary>dismissed は PullRequestReviewDismissed 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncDismissedUsesDismissedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "dismissed", "APPROVED");

        Assert.Equal(EmbedColors.PullRequestReviewDismissed, color);
    }

    /// <summary>
    /// 既知のバグ (B2): submitted + COMMENTED が Approved 色（緑）で表示されてしまう。
    /// 本来は専用の色（例: PullRequestReviewCommented）を使うべきだが、現在は
    /// <see cref="EmbedColors.PullRequestReviewApproved"/> が使われている。
    /// このテストは現在の（バグのある）挙動を文書化する。
    /// </summary>
    [Fact]
    public async Task RunAsyncSubmittedCommentedUsesApprovedColorIncorrectlyKnownBug()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // NOTE: これは既知のバグ (B2) です。
        // COMMENTED レビューが PullRequestReviewApproved (緑) で表示されてしまう。
        // 本来は専用の色（例: EmbedColors.PullRequestReviewCommented）を使うべき。
        // TODO (B2 修正時): このテストを削除し、EmbedColors.PullRequestReviewCommented 色を
        // 検証する新しいテストに差し替えること。
        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "COMMENTED");

        // バグの現在挙動を文書化: COMMENTED でも Approved 色が使われる
        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>approved アクションのタイトルに "approved" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncSubmittedApprovedTitleContainsApproved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("submitted", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("approved", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>changes_requested アクションのタイトルに "changes" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncSubmittedChangesRequestedTitleContainsChanges()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("submitted", "CHANGES_REQUESTED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("changes", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>dismissed アクションのタイトルに "dismissed" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncDismissedTitleContainsDismissed()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("dismissed", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("dismissed", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }
}
