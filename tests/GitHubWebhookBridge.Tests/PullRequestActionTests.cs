using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class PullRequestActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static PullRequest MakePullRequest(bool merged = false, bool draft = false) => new()
    {
        Number = 42,
        Title = "Add awesome feature",
        Body = "This PR adds an awesome feature.",
        State = merged ? "closed" : "open",
        HtmlUrl = new Uri("https://github.com/test/repo/pull/42"),
        User = new User { Login = "pr-author", Id = 100, HtmlUrl = new Uri("https://github.com/pr-author") },
        Draft = draft,
        Merged = merged ? true : null,
        Head = new PullRequestRef { Ref = "feature/my-branch" },
        Base = new PullRequestRef { Ref = "main" },
    };

    private static PullRequestEvent MakePrEvent(string action, bool merged = false, bool draft = false) => new()
    {
        Action = action,
        Number = 42,
        PullRequest = MakePullRequest(merged, draft),
        Repository = new Repository { FullName = "test/repo", HtmlUrl = new Uri("https://github.com/test/repo") },
        Sender = new User { Login = "sender", Id = 200, HtmlUrl = new Uri("https://github.com/sender") },
    };

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

    [Fact]
    public async Task RunAsyncSendsMessageForOpenedEvent()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("opened") &&
                    m.Embeds[0].Title!.Contains("#42"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesMergedColorWhenClosedAndMergedIsTrue()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            MakePrEvent("closed", merged: true), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // "merged" というテキストがタイトルに含まれることを確認する
        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("merged"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesClosedColorWhenClosedAndMergedIsFalse()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            MakePrEvent("closed", merged: false), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("closed"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncIncludesBranchInfoInFields()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("feature/my-branch") && f.Value.Contains("main")))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesPrNumberAsPartOfCacheKey()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(
            c => c.GetAsync(_webhookUri, "test/repo#42-opened"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncSendsMentionForReviewRequestedEvent()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        User reviewer = new() { Login = "reviewer-user", Id = 300 };
        PullRequestEvent prEvent = MakePrEvent("review_requested");
        prEvent.RequestedReviewer = reviewer;

        // レビュアーに Discord ID がマッピングされている場合
        userMap.Setup(u => u.GetById(300L)).Returns("discord-user-id-300");

        PullRequestAction action = new(
            discord.Object, _webhookUri, "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-user-id-300>"))),
            Times.Once);
    }
}
