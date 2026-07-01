using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for PublicAction's notification content and cache key.</summary>
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

    private static PublicEvent MakeEvent() => JsonSerializer.Deserialize<PublicEvent>(
        $$"""{"repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},"sender":{{TestFixtures.UserJson("publisher",1)}}}""",
        OctokitJsonOptions.Value)!;

    /// <summary>The title contains "Published", the repository name, and the sender login.</summary>
    [Fact]
    public async Task RunAsyncTitleContainsPublishedAndRepoAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PublicAction>>(),
            _webhookUri, "public", MakeEvent());

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

    /// <summary>The cache key contains the sender login.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PublicAction>>(),
            _webhookUri, "public", MakeEvent());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-public-publisher"), Times.Once);
    }
}
