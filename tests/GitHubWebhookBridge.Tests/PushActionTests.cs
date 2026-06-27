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
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static PushEvent MakePushEvent(List<Commit>? commits = null) => new()
    {
        Ref = "refs/heads/main",
        Before = "aaa",
        After = "bbb",
        Compare = "https://github.com/test/repo/compare/aaa...bbb",
        Repository = new Repository { FullName = "test/repo", HtmlUrl = new Uri("https://github.com/test/repo") },
        Sender = new User { Login = "octocat", Id = 1, HtmlUrl = new Uri("https://github.com/octocat") },
        Commits = commits ?? [
            new Commit
            {
                Id = "abcdef1234567890",
                Message = "feat: add new feature",
                Url = new Uri("https://github.com/test/repo/commit/abcdef1"),
                Author = new CommitAuthor { Name = "octocat" },
            },
        ],
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

        return (discord, cache, userMap);
    }

    [Fact]
    public async Task RunAsyncSendsMessageWithCommitInfo()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushAction action = new(
            discord.Object, _webhookUri, "push",
            MakePushEvent(), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("test/repo:main") &&
                    m.Embeds[0].Title!.Contains("1 new commit"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncDoesNotSendWhenNoCommits()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushAction action = new(
            discord.Object, _webhookUri, "push",
            MakePushEvent(commits: []), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // コミットがない場合はメッセージを送信しない
        discord.Verify(
            d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsyncStripsRefsHeadsPrefix()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushEvent pushEvent = MakePushEvent();
        pushEvent.Ref = "refs/heads/feature/my-branch";

        PushAction action = new(
            discord.Object, _webhookUri, "push",
            pushEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("feature/my-branch"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncTruncatesLongCommitMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        string longMessage = new('a', 60); // 60 文字 > 50 文字上限
        List<Commit> commits =
        [
            new() { Id = "abc1234", Message = longMessage, Url = new Uri("https://x.example.com/"), Author = new CommitAuthor { Name = "user" } },
        ];

        PushAction action = new(
            discord.Object, _webhookUri, "push",
            MakePushEvent(commits), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncOnlyUsesFirstLineOfMultiLineMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        List<Commit> commits =
        [
            new() { Id = "abc1234", Message = "first line\nsecond line\nthird", Url = new Uri("https://x.example.com/"), Author = new CommitAuthor { Name = "user" } },
        ];

        PushAction action = new(
            discord.Object, _webhookUri, "push",
            MakePushEvent(commits), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("first line") &&
                    !m.Embeds![0].Description!.Contains("second line"))),
            Times.Once);
    }
}
