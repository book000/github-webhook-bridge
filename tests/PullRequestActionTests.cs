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

public class PullRequestActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static PullRequestEvent MakePrEvent(
        string action,
        bool merged = false,
        bool draft = false,
        string? requestedReviewerLogin = null,
        long? requestedReviewerId = null) =>
        JsonSerializer.Deserialize<PullRequestEvent>(
            $$"""
            {
                "action":"{{action}}",
                "number":42,
                "pull_request":{{TestFixtures.PullRequestJson(
                    number: 42, title: "Add awesome feature",
                    htmlUrl: "https://github.com/test/repo/pull/42",
                    body: "This PR adds an awesome feature.",
                    userLogin: "pr-author", userId: 100,
                    draft: draft, merged: merged,
                    headRef: "feature/my-branch", baseRef: "main")}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("sender",200,"https://github.com/sender")}}
                {{(requestedReviewerLogin is not null
                    ? $",\"requested_reviewer\":{TestFixtures.UserJson(requestedReviewerLogin, requestedReviewerId ?? 300)}"
                    : string.Empty)}}
            }
            """,
            OctokitJsonOptions.Value)!;

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

    [Fact]
    public async Task RunAsyncSendsMessageForOpenedEvent()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", MakePrEvent("opened"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("opened") &&
                    m.Embeds[0].Title!.Contains("#42"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesMergedColorWhenClosedAndMergedIsTrue()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", MakePrEvent("closed", merged: true));

        await action.RunAsync();

        // Verify the title contains the text "merged"
        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("merged"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesClosedColorWhenClosedAndMergedIsFalse()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", MakePrEvent("closed", merged: false));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("closed"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncIncludesBranchInfoInFields()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", MakePrEvent("opened"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("feature/my-branch") && f.Value.Contains("main")))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncUsesPrNumberAsPartOfCacheKey()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", MakePrEvent("opened"));

        await action.RunAsync();

        cache.Verify(
            c => c.GetAsync(_webhookUri, "test/repo#42-opened"),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncSendsMentionForReviewRequestedEvent()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // When the reviewer is mapped to a Discord ID
        // WebhookConverter deserializes "review_requested" into a PullRequestReviewRequestedEvent
        userMap.Setup(u => u.GetById(300L)).Returns("discord-user-id-300");

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request",
            MakePrEvent("review_requested", requestedReviewerLogin: "reviewer-user", requestedReviewerId: 300));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-user-id-300>"))),
            Times.Once);
    }

    /// <summary>review_requested and review_request_removed share the common key suffix "review_requested".</summary>
    [Fact]
    public async Task RunAsyncReviewRequestedAndRemovedShareCacheKeySuffix()
    {
        (Mock<IDiscordClient>? discord1, Mock<IMessageCacheService>? cache1, Mock<IGitHubUserMapManager>? userMap1) = CreateMocks();
        (Mock<IDiscordClient>? discord2, Mock<IMessageCacheService>? cache2, Mock<IGitHubUserMapManager>? userMap2) = CreateMocks();

        PullRequestAction action1 = new(
            discord1.Object, cache1.Object, userMap1.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request",
            MakePrEvent("review_requested", requestedReviewerLogin: "reviewer-user", requestedReviewerId: 300));

        PullRequestAction action2 = new(
            discord2.Object, cache2.Object, userMap2.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request",
            MakePrEvent("review_request_removed", requestedReviewerLogin: "reviewer-user", requestedReviewerId: 300));

        await action1.RunAsync();
        await action2.RunAsync();

        // Both use the key with the "review_requested" suffix
        cache1.Verify(c => c.GetAsync(_webhookUri, "test/repo#42-review_requested"), Times.Once);
        cache2.Verify(c => c.GetAsync(_webhookUri, "test/repo#42-review_requested"), Times.Once);
    }

    /// <summary>No mention is sent when review_requested arrives for a draft PR.</summary>
    [Fact]
    public async Task RunAsyncDoesNotSendMentionForReviewRequestedOnDraftPr()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(300L)).Returns("discord-user-id-300");

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request",
            MakePrEvent("review_requested", draft: true, requestedReviewerLogin: "reviewer-user", requestedReviewerId: 300));

        await action.RunAsync();

        // No mention for a draft PR (Content is null or empty)
        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Content == null || m.Content == string.Empty)),
            Times.Once);
    }
}
