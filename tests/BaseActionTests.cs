using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for the edit-or-send behavior of <see cref="BaseAction{TEvent}"/> when Discord rate-limits.</summary>
public class BaseActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");
    private static readonly DiscordMessage _message = new("hello");

    private sealed class SendingAction(
        IDiscordClient discord,
        IMessageCacheService cache,
        IGitHubUserMapManager userMap)
        : BaseAction<WebhookEvent>(discord, _webhookUri, "test", Mock.Of<WebhookEvent>(), cache, userMap, Mock.Of<ILogger>())
    {
        public override Task RunAsync() => SendMessageAsync("key", _message);
    }

    private static (SendingAction Action, Mock<IDiscordClient> Discord, Mock<IMessageCacheService> Cache) Create()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        cache.Setup(c => c.GetAsync(_webhookUri, "key")).ReturnsAsync(new CachedMessage("existing-id"));
        return (new SendingAction(discord.Object, cache.Object, Mock.Of<IGitHubUserMapManager>()), discord, cache);
    }

    [Fact]
    public async Task SendMessageAsync_RateLimitedEdit_PropagatesWithoutDeletingCacheOrSending()
    {
        (SendingAction action, Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) = Create();
        DiscordRateLimitException rateLimit = new(TimeSpan.FromSeconds(3), isGlobal: false, scope: "user");
        discord.Setup(d => d.EditMessageAsync(_webhookUri, "existing-id", It.IsAny<DiscordMessage>())).ThrowsAsync(rateLimit);

        DiscordRateLimitException thrown = await Assert.ThrowsAsync<DiscordRateLimitException>(() => action.RunAsync());

        Assert.Same(rateLimit, thrown);
        cache.Verify(c => c.DeleteAsync(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never);
        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_OtherEditFailure_FallsBackToSendingANewMessage()
    {
        (SendingAction action, Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) = Create();
        discord.Setup(d => d.EditMessageAsync(_webhookUri, "existing-id", It.IsAny<DiscordMessage>()))
            .ThrowsAsync(new HttpRequestException("Discord API error: 404 Not Found"));
        discord.Setup(d => d.SendMessageAsync(_webhookUri, It.IsAny<DiscordMessage>())).ReturnsAsync("new-id");

        await action.RunAsync();

        cache.Verify(c => c.DeleteAsync(_webhookUri, "key"), Times.Once);
        cache.Verify(c => c.SetAsync(_webhookUri, "key", "new-id"), Times.Once);
    }
}
