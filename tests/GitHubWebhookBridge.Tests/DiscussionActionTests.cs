using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>DiscussionAction の通知内容・本文切り詰め・キャッシュキーテスト。</summary>
public class DiscussionActionTests
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

    private static DiscussionEvent MakeEvent(
        string action,
        string? discussionBody = "body",
        DiscussionComment? comment = null,
        Label? label = null,
        DiscussionCategory? newCategory = null) => new()
    {
        Action = action,
        Discussion = new Discussion
        {
            Number = 5,
            Title = "Great discussion",
            Body = discussionBody,
            HtmlUrl = new Uri("https://github.com/test/repo/discussions/5"),
            User = new User { Login = "author", Id = 1 },
            State = "open",
            Category = new DiscussionCategory { Name = "General", IsAnswerable = false },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "author", Id = 1 },
        Comment = comment,
        Label = label,
        Category = newCategory,
    };

    /// <summary>created イベントのタイトルに "created" と Discussion 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCreatedAndNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("created") &&
                    m.Embeds![0].Title!.Contains("#5"))),
            Times.Once);
    }

    /// <summary>created は DiscussionCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesDiscussionCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.DiscussionCreated, capturedColor);
    }

    /// <summary>answered イベントは discussion.Body ではなく comment.Body を description に使用する。</summary>
    [Fact]
    public async Task RunAsyncAnsweredUsesCommentBodyNotDiscussionBody()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var answerComment = new DiscussionComment
        {
            Id = 999,
            Body = "This is the answer",
            HtmlUrl = new Uri("https://github.com/test/repo/discussions/5#discussioncomment-999"),
            User = new User { Login = "answerer", Id = 2 },
        };

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("answered", discussionBody: "original body", comment: answerComment),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("This is the answer") &&
                    !m.Embeds![0].Description!.Contains("original body"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに Discussion 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsDiscussionNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-discussion-5"), Times.Once);
    }

    /// <summary>category_changed イベントは新カテゴリ名をフィールドに含む。</summary>
    [Fact]
    public async Task RunAsyncCategoryChangedIncludesNewCategoryField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("category_changed", newCategory: new DiscussionCategory { Name = "Q&A" }),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("Q&A")))),
            Times.Once);
    }
}
