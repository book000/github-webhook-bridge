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
    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        var discord   = new Mock<IDiscordClient>();
        var cache     = new Mock<IMessageCacheService>();
        var userMap   = new Mock<IGitHubUserMapManager>();

        // キャッシュはデフォルトで null（新規送信）を返す
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        // Discord 送信は常にダミーのメッセージ ID を返す
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("test-message-id");

        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    [Fact]
    public async Task RunAsync_SendsMessageWithPingEmbed()
    {
        var (discord, cache, userMap) = CreateMocks();

        var pingEvent = new PingEvent
        {
            Zen    = "Non-blocking is better than blocking.",
            HookId = 12345,
        };

        var action = new PingAction(
            discord.Object, "https://discord.test/webhook", "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // Discord にメッセージが送信されたことを確認する
        discord.Verify(
            d => d.SendMessageAsync(
                "https://discord.test/webhook",
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title == "Received a ping event" &&
                    m.Embeds[0].Description == "Non-blocking is better than blocking.")),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesHookIdAsKeyForCache()
    {
        var (discord, cache, userMap) = CreateMocks();

        var pingEvent = new PingEvent { Zen = "test", HookId = 9999 };

        var action = new PingAction(
            discord.Object, "https://discord.test/webhook", "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // キャッシュキーが hook_id を含むことを確認する
        cache.Verify(
            c => c.GetAsync("https://discord.test/webhook", "ping-9999"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_EditsMessageWhenCachedMessageExists()
    {
        var (discord, cache, userMap) = CreateMocks();

        // キャッシュにメッセージが存在する場合
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync(new CachedMessage("existing-message-id"));

        var pingEvent = new PingEvent { Zen = "Speak friend and enter.", HookId = 1 };

        var action = new PingAction(
            discord.Object, "https://discord.test/webhook", "ping",
            pingEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        // 新規送信ではなく編集が呼ばれることを確認する
        discord.Verify(
            d => d.EditMessageAsync(
                "https://discord.test/webhook",
                "existing-message-id",
                It.IsAny<DiscordMessage>()),
            Times.Once);
        discord.Verify(
            d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()),
            Times.Never);
    }
}
