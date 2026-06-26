using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class PushActionTests
{
    private static PushEvent MakePushEvent(List<Commit>? commits = null) => new()
    {
        Ref        = "refs/heads/main",
        Before     = "aaa",
        After      = "bbb",
        Compare    = "https://github.com/test/repo/compare/aaa...bbb",
        Repository = new Repository { FullName = "test/repo", HtmlUrl = "https://github.com/test/repo" },
        Sender     = new User { Login = "octocat", Id = 1, HtmlUrl = "https://github.com/octocat" },
        Commits    = commits ?? [
            new Commit
            {
                Id      = "abcdef1234567890",
                Message = "feat: add new feature",
                Url     = "https://github.com/test/repo/commit/abcdef1",
                Author  = new CommitAuthor { Name = "octocat" },
            },
        ],
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

        return (discord, cache, userMap);
    }

    [Fact]
    public async Task RunAsync_SendsMessageWithCommitInfo()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PushAction(
            discord.Object, "https://discord.test/webhook", "push",
            MakePushEvent(), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("test/repo:main") &&
                    m.Embeds[0].Title!.Contains("1 new commit"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DoesNotSendWhenNoCommits()
    {
        var (discord, cache, userMap) = CreateMocks();

        var action = new PushAction(
            discord.Object, "https://discord.test/webhook", "push",
            MakePushEvent(commits: []), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // コミットがない場合はメッセージを送信しない
        discord.Verify(
            d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_StripsRefsHeadsPrefix()
    {
        var (discord, cache, userMap) = CreateMocks();

        var pushEvent = MakePushEvent();
        pushEvent.Ref = "refs/heads/feature/my-branch";

        var action = new PushAction(
            discord.Object, "https://discord.test/webhook", "push",
            pushEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("feature/my-branch"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_TruncatesLongCommitMessage()
    {
        var (discord, cache, userMap) = CreateMocks();

        var longMessage = new string('a', 60); // 60 文字 > 50 文字上限
        var commits = new List<Commit>
        {
            new() { Id = "abc1234", Message = longMessage, Url = "https://x", Author = new CommitAuthor { Name = "user" } },
        };

        var action = new PushAction(
            discord.Object, "https://discord.test/webhook", "push",
            MakePushEvent(commits), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_OnlyUsesFirstLineOfMultiLineMessage()
    {
        var (discord, cache, userMap) = CreateMocks();

        var commits = new List<Commit>
        {
            new() { Id = "abc1234", Message = "first line\nsecond line\nthird", Url = "https://x", Author = new CommitAuthor { Name = "user" } },
        };

        var action = new PushAction(
            discord.Object, "https://discord.test/webhook", "push",
            MakePushEvent(commits), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<string>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("first line") &&
                    !m.Embeds![0].Description!.Contains("second line"))),
            Times.Once);
    }
}
