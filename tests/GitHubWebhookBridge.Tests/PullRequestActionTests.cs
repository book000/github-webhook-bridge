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
    private static PullRequest MakePullRequest(bool merged = false, bool draft = false) => new()
    {
        Number  = 42,
        Title   = "Add awesome feature",
        Body    = "This PR adds an awesome feature.",
        State   = merged ? "closed" : "open",
        HtmlUrl = "https://github.com/test/repo/pull/42",
        User    = new User { Login = "pr-author", Id = 100, HtmlUrl = "https://github.com/pr-author" },
        Draft   = draft,
        Merged  = merged ? true : null,
        Head    = new PullRequestRef { Ref = "feature/my-branch" },
        Base    = new PullRequestRef { Ref = "main" },
    };

    private static PullRequestEvent MakePrEvent(string action, bool merged = false, bool draft = false) => new()
    {
        Action      = action,
        Number      = 42,
        PullRequest = MakePullRequest(merged, draft),
        Repository  = new Repository { FullName = "test/repo", HtmlUrl = "https://github.com/test/repo" },
        Sender      = new User { Login = "sender", Id = 200, HtmlUrl = "https://github.com/sender" },
    };

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

    [Fact]
    public async Task RunAsync_SendsMessageForOpenedEvent()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("opened") &&
                    m.Embeds[0].Title!.Contains("#42"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesMergedColorWhenClosed_AndMergedIsTrue()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            MakePrEvent("closed", merged: true), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // "merged" というテキストがタイトルに含まれることを確認する
        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("merged"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesClosedColorWhenClosed_AndMergedIsFalse()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            MakePrEvent("closed", merged: false), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("closed"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_IncludesBranchInfoInFields()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("feature/my-branch") && f.Value.Contains("main")))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesPrNumberAsPartOfCacheKey()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            MakePrEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(
            c => c.GetAsync("https://discord.test/webhook", "test/repo-pr-42"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_SendsMentionForReviewRequestedEvent()
    {
        var (discord, cache, userMap) = CreateMocks();

        var reviewer = new User { Login = "reviewer-user", Id = 300 };
        var prEvent  = MakePrEvent("review_requested");
        prEvent.RequestedReviewer = reviewer;

        // レビュアーに Discord ID がマッピングされている場合
        userMap.Setup(u => u.Get(300L)).Returns("discord-user-id-300");

        var action = new PullRequestAction(
            discord.Object, "https://discord.test/webhook", "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-user-id-300>"))),
            Times.Once);
    }
}
