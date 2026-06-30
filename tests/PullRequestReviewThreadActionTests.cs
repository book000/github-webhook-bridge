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

/// <summary>PullRequestReviewThreadAction の通知内容・色・キャッシュキーテスト。</summary>
public class PullRequestReviewThreadActionTests
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

    /// <summary>
    /// PullRequestReviewThreadEvent を JSON から生成する。
    /// Octokit の PullRequestReviewThreadEvent には Thread プロパティが存在しないため、
    /// Review.NodeId をスレッド識別子として使用する。
    /// </summary>
    private static PullRequestReviewThreadEvent MakeEvent(string action) =>
        JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"{{action}}",
                "review":{{TestFixtures.ReviewJson(
                    1, "approved",
                    "https://github.com/test/repo/pull/12#pullrequestreview-1",
                    nodeId: "RT_node_abc")}},
                "pull_request":{{TestFixtures.SimplePrJson(
                    12, "Feature branch",
                    "https://github.com/test/repo/pull/12",
                    "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>resolved イベントのタイトルに "resolved" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncResolvedTitleContainsResolved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("resolved"))),
            Times.Once);
    }

    /// <summary>resolved は PullRequestReviewThreadResolved 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncResolvedUsesPrReviewThreadResolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadResolved, capturedColor);
    }

    /// <summary>unresolved は PullRequestReviewThreadUnresolved 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncUnresolvedUsesPrReviewThreadUnresolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("unresolved"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadUnresolved, capturedColor);
    }

    /// <summary>Embed フィールドにスレッドの NodeId "RT_node_abc" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncEmbedFieldContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("RT_node_abc")))),
            Times.Once);
    }

    /// <summary>キャッシュキーにスレッドの NodeId が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-thread-RT_node_abc"), Times.Once);
    }

    /// <summary>PR 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsPrAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(50L)).Returns("discord-author-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-author-id>"))),
            Times.Once);
    }
}
