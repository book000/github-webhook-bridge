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

/// <summary>PullRequestReviewCommentAction の通知内容・diff フィールド・メンション・キャッシュキーテスト。</summary>
public class PullRequestReviewCommentActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

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

    private static PullRequestReviewCommentEvent MakeEvent(
        string action = "created",
        string? path = null,
        long commentId = 5001) =>
        JsonSerializer.Deserialize<PullRequestReviewCommentEvent>(
            $$"""
            {
                "action":"{{action}}",
                "comment":{{TestFixtures.ReviewCommentJson(
                    commentId, "Looks good!",
                    path ?? "src/file.cs",
                    $"https://github.com/test/repo/pull/10#discussion_r{commentId}")}},
                "pull_request":{{TestFixtures.SimplePrJson(
                    10, "My PR",
                    "https://github.com/test/repo/pull/10",
                    "pr-author", 20)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",30)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>created イベントのタイトルに "commented on" と PR 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCommentedOnAndPrNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent("created"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("commented on") &&
                    m.Embeds![0].Title!.Contains("#10"))),
            Times.Once);
    }

    /// <summary>created は PullRequestReviewCommentCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesPrReviewCommentCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent("created"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewCommentCreated, capturedColor);
    }

    /// <summary>TestFixtures.ReviewCommentJson は常に diff_hunk を含むため diff フィールドが追加される。</summary>
    [Fact]
    public async Task RunAsyncWithDiffHunkAddsDiffField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("```diff")))),
            Times.Once);
    }

    /// <summary>path が設定されると Embed フィールドにファイルパスが追加される。</summary>
    [Fact]
    public async Task RunAsyncWithPathAddsFileField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent(path: "src/main.cs"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("src/main.cs")))),
            Times.Once);
    }

    /// <summary>キャッシュキーにコメント ID が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsCommentId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent(commentId: 5001));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-comment-5001"), Times.Once);
    }

    /// <summary>PR 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsPrAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(20L)).Returns("discord-pr-author-id");

        PullRequestReviewCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewCommentAction>>(),
            _webhookUri, "pull_request_review_comment", MakeEvent("created"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-pr-author-id>"))),
            Times.Once);
    }
}
