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

/// <summary>Tests for ForkAction notification content and cache keys.</summary>
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

    private static ForkEvent MakeEvent() => JsonSerializer.Deserialize<ForkEvent>(
        $$"""{"repository":{{TestFixtures.RepoJson("original/repo","https://github.com/original/repo")}},"forkee":{{TestFixtures.RepoJson("forker/repo","https://github.com/forker/repo")}},"sender":{{TestFixtures.UserJson("forker",1)}}}""",
        OctokitJsonOptions.Value)!;

    /// <summary>The title contains the source and forkee repository names and the sender login.</summary>
    [Fact]
    public async Task RunAsyncTitleContainsSourceForkeeAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<ForkAction>>(),
            _webhookUri, "fork", MakeEvent());

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

    /// <summary>The cache key contains the sender login.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<ForkAction>>(),
            _webhookUri, "fork", MakeEvent());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "original/repo-fork-forker"), Times.Once);
    }
}
