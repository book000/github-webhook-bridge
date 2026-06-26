using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class PingActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        // キャッシュはデフォルトで null（新規送信）を返す
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        // Discord 送信は常にダミーのメッセージ ID を返す
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("test-message-id");

        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    [Fact]
    public async Task RunAsyncSendsMessageWithPingEmbed()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PingEvent pingEvent = new()
        {
            Zen = "Non-blocking is better than blocking.",
            HookId = 12345,
        };

        PingAction action = new(
            discord.Object, _webhookUri, "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // Discord にメッセージが送信されたことを確認する
        discord.Verify(
            d => d.SendMessageAsync(
                _webhookUri,
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title == "Received a ping event" &&
                    m.Embeds[0].Description == "Non-blocking is better than blocking.")),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesCompositeKeyForCache()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PingEvent pingEvent = new()
        {
            Zen = "test",
            HookId = 9999,
            Hook = new PingHook { Type = "Repository" },
            Repository = new Repository { FullName = "owner/repo" },
            Sender = new User { Login = "user1" },
        };

        PingAction action = new(
            discord.Object, _webhookUri, "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // キャッシュキーがリポジトリ・送信者・フックタイプの複合キーであることを確認する
        cache.Verify(
            c => c.GetAsync(_webhookUri, "ping:owner/repo:user1:N/A:Repository"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncEditsMessageWhenCachedMessageExists()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // キャッシュにメッセージが存在する場合
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync(new CachedMessage("existing-message-id"));

        PingEvent pingEvent = new() { Zen = "Speak friend and enter.", HookId = 1 };

        PingAction action = new(
            discord.Object, _webhookUri, "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // 新規送信ではなく編集が呼ばれることを確認する
        discord.Verify(
            d => d.EditMessageAsync(
                _webhookUri,
                "existing-message-id",
                It.IsAny<DiscordMessage>()),
            Times.Once);
        discord.Verify(
            d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()),
            Times.Never);
    }
}
