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

/// <summary>StarAction の通知内容・色・キャッシュキーテスト。</summary>
public class StarActionTests
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

    private static StarEvent MakeEvent(string action) => JsonSerializer.Deserialize<StarEvent>(
        $$"""{"action":"{{action}}","repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},"sender":{{TestFixtures.UserJson("stargazer",1)}}}""",
        OctokitJsonOptions.Value)!;

    /// <summary>created → "Starred" というタイトルになる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsStarred()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<StarAction>>(),
            _webhookUri, "star", MakeEvent("created"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("Starred"))),
            Times.Once);
    }

    /// <summary>deleted → "Unstarred" というタイトルになる。</summary>
    [Fact]
    public async Task RunAsyncDeletedTitleContainsUnstarred()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<StarAction>>(),
            _webhookUri, "star", MakeEvent("deleted"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("Unstarred"))),
            Times.Once);
    }

    /// <summary>created → Star 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesStarColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        StarAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<StarAction>>(),
            _webhookUri, "star", MakeEvent("created"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.Star, capturedColor);
    }

    /// <summary>deleted → Unstar 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncDeletedUsesUnstarColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        StarAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<StarAction>>(),
            _webhookUri, "star", MakeEvent("deleted"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.Unstar, capturedColor);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<StarAction>>(),
            _webhookUri, "star", MakeEvent("created"));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-star-stargazer"), Times.Once);
    }
}
