using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

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

    private static PullRequestReviewThreadEvent MakeEvent(string action, bool resolved = true) => new()
    {
        Action = action,
        Thread = new ReviewThread
        {
            NodeId = "RT_node_abc",
            Resolved = resolved,
        },
        PullRequest = new PullRequest
        {
            Number = 12,
            Title = "Feature branch",
            State = "open",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/12"),
            User = new User { Login = "pr-author", Id = 50 },
            Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
            Base = new PullRequestRef { Ref = "main", Sha = "def" },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "reviewer", Id = 60 },
    };

    /// <summary>resolved イベントのタイトルに "resolved" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncResolvedTitleContainsResolved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("unresolved", resolved: false), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadUnresolved, capturedColor);
    }

    /// <summary>Embed フィールドにスレッドの NodeId が含まれる。</summary>
    [Fact]
    public async Task RunAsyncEmbedFieldContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
