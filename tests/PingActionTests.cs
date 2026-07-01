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

public class PingActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        // Cache returns null by default (new message send)
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        // Discord send always returns a dummy message ID
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("test-message-id");

        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    /// <summary>Helper that creates a PingEvent by deserializing a minimal PingEvent JSON.</summary>
    private static PingEvent MakePingEvent(
        string zen = "Non-blocking is better than blocking.",
        long hookId = 12345,
        string? repoFullName = null,
        string? senderLogin = null,
        string? hookType = null)
    {
        var repoPart = repoFullName is not null
            ? $",\"repository\":{TestFixtures.RepoJson(repoFullName)}"
            : string.Empty;
        var senderPart = senderLogin is not null
            ? $",\"sender\":{TestFixtures.UserJson(senderLogin, 1)}"
            : string.Empty;
        // Hook.Type is required, so use the default "Repository"
        var hookTypeStr = hookType ?? "Repository";
        var hookJson = "{\"id\":1,\"type\":\"" + hookTypeStr + "\",\"name\":\"web\",\"active\":true,\"events\":[\"push\"],\"config\":{\"url\":\"https://example.com\",\"content_type\":\"json\",\"insecure_ssl\":\"0\"},\"updated_at\":\"2024-01-01T00:00:00Z\",\"created_at\":\"2024-01-01T00:00:00Z\",\"url\":\"https://api.github.com/repos/owner/repo/hooks/1\",\"test_url\":\"https://api.github.com/repos/owner/repo/hooks/1/test\",\"ping_url\":\"https://api.github.com/repos/owner/repo/hooks/1/pings\",\"deliveries_url\":\"https://api.github.com/repos/owner/repo/hooks/1/deliveries\"}";
        return JsonSerializer.Deserialize<PingEvent>(
            "{\"zen\":\"" + zen + "\",\"hook_id\":" + hookId + ",\"hook\":" + hookJson + repoPart + senderPart + "}",
            OctokitJsonOptions.Value)!;
    }

    [Fact]
    public async Task RunAsyncSendsMessageWithPingEmbed()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PingAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PingAction>>(),
            _webhookUri, "ping", MakePingEvent(zen: "Non-blocking is better than blocking.", hookId: 12345));

        await action.RunAsync();

        // Verify a message was sent to Discord
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

        PingAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PingAction>>(),
            _webhookUri, "ping",
            MakePingEvent(hookId: 9999, repoFullName: "owner/repo", senderLogin: "user1", hookType: "Repository"));

        await action.RunAsync();

        // Verify the cache key is a composite of repository, sender, and hook type
        cache.Verify(
            c => c.GetAsync(_webhookUri, "ping:owner/repo:user1:N/A:Repository"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncEditsMessageWhenCachedMessageExists()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // When a message already exists in the cache
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync(new CachedMessage("existing-message-id"));

        PingAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PingAction>>(),
            _webhookUri, "ping", MakePingEvent(zen: "Speak friend and enter.", hookId: 1));

        await action.RunAsync();

        // Verify edit is called instead of a new send
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
