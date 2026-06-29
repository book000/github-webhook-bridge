using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>ForkAction の通知内容・キャッシュキーテスト。</summary>
public class ForkActionTests
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

    private static ForkEvent MakeEvent() => new()
    {
        Repository = new Repository
        {
            FullName = "original/repo",
            HtmlUrl = new Uri("https://github.com/original/repo"),
        },
        Forkee = new Repository
        {
            FullName = "forker/repo",
            HtmlUrl = new Uri("https://github.com/forker/repo"),
        },
        Sender = new User { Login = "forker", Id = 1 },
    };

    /// <summary>タイトルにフォーク元・フォーク先リポジトリ名と送信者 login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncTitleContainsSourceForkeeAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, _webhookUri, "fork",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("original/repo") &&
                    m.Embeds![0].Title!.Contains("forker/repo") &&
                    m.Embeds![0].Title!.Contains("forker"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, _webhookUri, "fork",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "original/repo-fork-forker"), Times.Once);
    }
}
