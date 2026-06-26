using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>PullRequestReviewAction の色・タイトル検証テスト。</summary>
public class PullRequestReviewActionTests
{
    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        var discord = new Mock<IDiscordClient>();
        var cache   = new Mock<IMessageCacheService>();
        var userMap = new Mock<IGitHubUserMapManager>();

        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    /// <summary>テスト用 PullRequestReviewEvent を JSON から生成する。</summary>
    private static PullRequestReviewEvent MakePrReviewEvent(string action, string state)
    {
        var json = $$"""
        {
            "action": "{{action}}",
            "review": {
                "id": 1,
                "state": "{{state}}",
                "body": "LGTM",
                "html_url": "https://github.com/owner/repo/pull/1#pullrequestreview-1",
                "user": {
                    "id": 1,
                    "login": "reviewer",
                    "html_url": "https://github.com/reviewer",
                    "avatar_url": "https://avatars.githubusercontent.com/u/1"
                }
            },
            "pull_request": {
                "id": 1,
                "number": 1,
                "title": "Test PR",
                "html_url": "https://github.com/owner/repo/pull/1",
                "head": {"ref": "feature", "sha": "abc123"},
                "base": {"ref": "main", "sha": "def456"},
                "draft": false,
                "body": null,
                "state": "open",
                "user": {
                    "id": 2,
                    "login": "pr-author",
                    "html_url": "https://github.com/pr-author",
                    "avatar_url": "https://avatars.githubusercontent.com/u/2"
                }
            },
            "sender": {
                "id": 1,
                "login": "reviewer",
                "html_url": "https://github.com/reviewer",
                "avatar_url": "https://avatars.githubusercontent.com/u/1"
            },
            "repository": {
                "id": 1,
                "full_name": "owner/repo",
                "html_url": "https://github.com/owner/repo",
                "name": "repo"
            }
        }
        """;
        return JsonSerializer.Deserialize<PullRequestReviewEvent>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    /// <summary>キャプチャされた Embed の色を取得するヘルパー。</summary>
    private static async Task<int> RunAndCaptureColor(
        Mock<IDiscordClient> discord,
        Mock<IMessageCacheService> cache,
        Mock<IGitHubUserMapManager> userMap,
        string action,
        string state)
    {
        var prEvent = MakePrReviewEvent(action, state);

        var reviewAction = new PullRequestReviewAction(
            discord.Object,
            "https://discord.com/api/webhooks/1/x",
            "pull_request_review",
            prEvent,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger>());

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .Callback<string, DiscordMessage>((_, msg) =>
               {
                   capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1;
               })
               .ReturnsAsync("msg-id");

        await reviewAction.RunAsync();
        return capturedColor;
    }

    /// <summary>submitted + APPROVED のレビューは PullRequestReviewApproved 色を使用する。</summary>
    [Fact]
    public async Task RunAsync_SubmittedApproved_UsesApprovedColor()
    {
        var (discord, cache, userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "APPROVED");

        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>submitted + CHANGES_REQUESTED は PullRequestReviewChangesRequested 色を使用する。</summary>
    [Fact]
    public async Task RunAsync_SubmittedChangesRequested_UsesChangesRequestedColor()
    {
        var (discord, cache, userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "CHANGES_REQUESTED");

        Assert.Equal(EmbedColors.PullRequestReviewChangesRequested, color);
    }

    /// <summary>dismissed は PullRequestReviewDismissed 色を使用する。</summary>
    [Fact]
    public async Task RunAsync_Dismissed_UsesDismissedColor()
    {
        var (discord, cache, userMap) = CreateMocks();

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
    public async Task RunAsync_SubmittedCommented_UsesApprovedColorIncorrectly_KnownBug()
    {
        var (discord, cache, userMap) = CreateMocks();

        // NOTE: これは既知のバグ (B2) です。
        // COMMENTED レビューが PullRequestReviewApproved (緑) で表示されてしまう。
        // 本来は専用の色（例: EmbedColors.PullRequestReviewCommented）を使うべき。
        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "COMMENTED");

        // バグの現在挙動を文書化: COMMENTED でも Approved 色が使われる
        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>approved アクションのタイトルに "approved" が含まれる。</summary>
    [Fact]
    public async Task RunAsync_SubmittedApproved_TitleContainsApproved()
    {
        var (discord, cache, userMap) = CreateMocks();
        var prEvent = MakePrReviewEvent("submitted", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .Callback<string, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        var action = new PullRequestReviewAction(
            discord.Object,
            "https://discord.com/api/webhooks/1/x",
            "pull_request_review",
            prEvent,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("approved", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>changes_requested アクションのタイトルに "changes" が含まれる。</summary>
    [Fact]
    public async Task RunAsync_SubmittedChangesRequested_TitleContainsChanges()
    {
        var (discord, cache, userMap) = CreateMocks();
        var prEvent = MakePrReviewEvent("submitted", "CHANGES_REQUESTED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .Callback<string, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        var action = new PullRequestReviewAction(
            discord.Object,
            "https://discord.com/api/webhooks/1/x",
            "pull_request_review",
            prEvent,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("changes", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>dismissed アクションのタイトルに "dismissed" が含まれる。</summary>
    [Fact]
    public async Task RunAsync_Dismissed_TitleContainsDismissed()
    {
        var (discord, cache, userMap) = CreateMocks();
        var prEvent = MakePrReviewEvent("dismissed", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .Callback<string, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        var action = new PullRequestReviewAction(
            discord.Object,
            "https://discord.com/api/webhooks/1/x",
            "pull_request_review",
            prEvent,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("dismissed", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }
}
