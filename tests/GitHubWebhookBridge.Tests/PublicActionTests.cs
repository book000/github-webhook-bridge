using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>PublicAction の通知内容・キャッシュキーテスト。</summary>
public class PublicActionTests
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

    private static PublicEvent MakeEvent() => new()
    {
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "publisher", Id = 1 },
    };

    /// <summary>タイトルに "Published"・リポジトリ名・送信者 login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncTitleContainsPublishedAndRepoAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, _webhookUri, "public",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("Published") &&
                    m.Embeds![0].Title!.Contains("test/repo") &&
                    m.Embeds![0].Title!.Contains("publisher"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, _webhookUri, "public",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-public-publisher"), Times.Once);
    }
}
