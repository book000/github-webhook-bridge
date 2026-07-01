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

/// <summary>Tests that verify PullRequestReviewAction's color and title.</summary>
public class PullRequestReviewActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.com/api/webhooks/1/x");

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

    /// <summary>Creates a test PullRequestReviewEvent from TestFixtures.</summary>
    private static PullRequestReviewEvent MakePrReviewEvent(string action, string reviewState) =>
        JsonSerializer.Deserialize<PullRequestReviewEvent>(
            $$"""
            {
                "action":"{{action}}",
                "review":{{TestFixtures.ReviewJson(1, reviewState.ToLowerInvariant(),
                    "https://github.com/owner/repo/pull/1#pullrequestreview-1")}},
                "pull_request":{{TestFixtures.SimplePrJson(
                    1, "Test PR",
                    "https://github.com/owner/repo/pull/1",
                    "pr-author", 2)}},
                "repository":{{TestFixtures.RepoJson("owner/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",1)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>Helper that captures and returns the color of the sent Embed.</summary>
    private static async Task<int> RunAndCaptureColor(
        Mock<IDiscordClient> discord,
        Mock<IMessageCacheService> cache,
        Mock<IGitHubUserMapManager> userMap,
        string action,
        string state)
    {
        PullRequestReviewEvent prEvent = MakePrReviewEvent(action, state);

        PullRequestReviewAction reviewAction = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        var capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1;
               })
               .ReturnsAsync("msg-id");

        await reviewAction.RunAsync();
        return capturedColor;
    }

    /// <summary>A submitted + APPROVED review uses the PullRequestReviewApproved color.</summary>
    [Fact]
    public async Task RunAsyncSubmittedApprovedUsesApprovedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "APPROVED");

        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>submitted + CHANGES_REQUESTED uses the PullRequestReviewChangesRequested color.</summary>
    [Fact]
    public async Task RunAsyncSubmittedChangesRequestedUsesChangesRequestedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "CHANGES_REQUESTED");

        Assert.Equal(EmbedColors.PullRequestReviewChangesRequested, color);
    }

    /// <summary>dismissed uses the PullRequestReviewDismissed color.</summary>
    [Fact]
    public async Task RunAsyncDismissedUsesDismissedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var color = await RunAndCaptureColor(discord, cache, userMap, "dismissed", "APPROVED");

        Assert.Equal(EmbedColors.PullRequestReviewDismissed, color);
    }

    /// <summary>
    /// Known bug (B2): submitted + COMMENTED is displayed with the Approved color (green).
    /// It should use a dedicated color (e.g. PullRequestReviewCommented), but currently
    /// <see cref="EmbedColors.PullRequestReviewApproved"/> is used.
    /// This test documents the current (buggy) behavior.
    /// </summary>
    [Fact]
    public async Task RunAsyncSubmittedCommentedUsesApprovedColorIncorrectlyKnownBug()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // NOTE: This is a known bug (B2).
        // A COMMENTED review is displayed with PullRequestReviewApproved (green).
        // It should use a dedicated color (e.g. EmbedColors.PullRequestReviewCommented).
        // TODO (when fixing B2): remove this test and replace it with a new test that
        // verifies the EmbedColors.PullRequestReviewCommented color.
        var color = await RunAndCaptureColor(discord, cache, userMap, "submitted", "COMMENTED");

        // Document the current buggy behavior: COMMENTED also uses the Approved color
        Assert.Equal(EmbedColors.PullRequestReviewApproved, color);
    }

    /// <summary>The title of the approved action contains "approved".</summary>
    [Fact]
    public async Task RunAsyncSubmittedApprovedTitleContainsApproved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("submitted", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("approved", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The title of the changes_requested action contains "changes".</summary>
    [Fact]
    public async Task RunAsyncSubmittedChangesRequestedTitleContainsChanges()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("submitted", "CHANGES_REQUESTED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("changes", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The title of the dismissed action contains "dismissed".</summary>
    [Fact]
    public async Task RunAsyncDismissedTitleContainsDismissed()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestReviewEvent prEvent = MakePrReviewEvent("dismissed", "APPROVED");

        string? capturedTitle = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) =>
               {
                   capturedTitle = msg.Embeds?.FirstOrDefault()?.Title;
               })
               .ReturnsAsync("msg-id");

        PullRequestReviewAction action = new(
            discord.Object,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger<PullRequestReviewAction>>(),
            _webhookUri,
            "pull_request_review",
            prEvent);

        await action.RunAsync();

        Assert.NotNull(capturedTitle);
        Assert.Contains("dismissed", capturedTitle, StringComparison.OrdinalIgnoreCase);
    }
}
