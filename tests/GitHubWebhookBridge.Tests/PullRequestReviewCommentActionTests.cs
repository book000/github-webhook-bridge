using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

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
        string? diffHunk = null,
        string? path = null,
        long commentId = 5001) => new()
    {
        Action = action,
        Comment = new ReviewComment
        {
            Id = commentId,
            Body = "Looks good!",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/10#discussion_r5001"),
            User = new User { Login = "reviewer", Id = 30 },
            DiffHunk = diffHunk,
            Path = path,
        },
        PullRequest = new PullRequest
        {
            Number = 10,
            Title = "My PR",
            State = "open",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/10"),
            User = new User { Login = "pr-author", Id = 20 },
            Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
            Base = new PullRequestRef { Ref = "main", Sha = "def" },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "reviewer", Id = 30 },
    };

    /// <summary>created イベントのタイトルに "commented on" と PR 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCommentedOnAndPrNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewCommentCreated, capturedColor);
    }

    /// <summary>diff_hunk が設定されると Embed フィールドに diff ブロックが追加される。</summary>
    [Fact]
    public async Task RunAsyncWithDiffHunkAddsDiffField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(diffHunk: "@@ -1,3 +1,4 @@ line"),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(path: "src/main.cs"),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(commentId: 5001), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

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
