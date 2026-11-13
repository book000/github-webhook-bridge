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

/// <summary>Tests for PullRequestReviewThreadAction's notification content, color, and cache key.</summary>
public class PullRequestReviewThreadActionTests
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

    /// <summary>
    /// Creates a PullRequestReviewThreadEvent from JSON.
    /// Faithfully reproduces the shape of the real GitHub payload ("thread", not "review").
    /// </summary>
    private static PullRequestReviewThreadEvent MakeEvent(string action) =>
        JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"{{action}}",
                "thread":{{TestFixtures.ThreadJson(nodeId: "RT_node_abc")}},
                "pull_request":{{TestFixtures.SimplePrJson(
                    12, "Feature branch",
                    "https://github.com/test/repo/pull/12",
                    "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>The title of the resolved event contains "resolved".</summary>
    [Fact]
    public async Task RunAsyncResolvedTitleContainsResolved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("resolved"))),
            Times.Once);
    }

    /// <summary>resolved uses the PullRequestReviewThreadResolved color.</summary>
    [Fact]
    public async Task RunAsyncResolvedUsesPrReviewThreadResolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadResolved, capturedColor);
    }

    /// <summary>unresolved uses the PullRequestReviewThreadUnresolved color.</summary>
    [Fact]
    public async Task RunAsyncUnresolvedUsesPrReviewThreadUnresolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("unresolved"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadUnresolved, capturedColor);
    }

    /// <summary>The embed shows the first comment's path, body, and links to the comment URL, without the raw node_id.</summary>
    [Fact]
    public async Task RunAsyncEmbedShowsFirstCommentPathBodyAndLink()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        DiscordEmbed embed = captured!.Embeds![0];
        Assert.Equal("src/file.cs", embed.Fields!.Single(f => f.Name == "File").Value);
        Assert.Equal("Great!", embed.Description);
        Assert.Equal("https://github.com/owner/repo/pull/1#discussion_r1", embed.Url!.ToString());
        Assert.DoesNotContain(embed.Fields!, f => f.Value.Contains("RT_node_abc"));
    }

    /// <summary>A comment body longer than 500 characters is truncated with an ellipsis.</summary>
    [Fact]
    public async Task RunAsyncTruncatesLongCommentBody()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadEvent evt = JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"resolved",
                "thread":{{TestFixtures.ThreadJson(commentJson: TestFixtures.ReviewCommentJson(body: new string('a', 600)))}},
                "pull_request":{{TestFixtures.SimplePrJson(12, "Feature branch", "https://github.com/test/repo/pull/12", "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer", 60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", evt);

        await action.RunAsync();

        Assert.Equal(new string('a', 500) + "...", captured!.Embeds![0].Description);
    }

    /// <summary>The vendored real payload yields the README.md path and the #discussion_r URL.</summary>
    [Fact]
    public async Task RunAsyncRealPayloadShowsPathAndDiscussionUrl()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        var json = await File.ReadAllTextAsync(Path.Combine(
            Path.GetDirectoryName(typeof(PullRequestReviewThreadActionTests).Assembly.Location)!,
            "RealPayloads",
            "pull_request_review_thread.resolved.json"));
        PullRequestReviewThreadEvent evt = JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(json, OctokitJsonOptions.Value)!;

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", evt);

        await action.RunAsync();

        DiscordEmbed embed = captured!.Embeds![0];
        Assert.Equal("README.md", embed.Fields!.Single(f => f.Name == "File").Value);
        Assert.Equal("https://github.com/Codertocat/Hello-World/pull/2#discussion_r284312630", embed.Url!.ToString());
        Assert.Equal("Maybe you should use more emoji on this line.", embed.Description);
    }

    /// <summary>Without any comments in the thread, the embed omits the file and links to the PR page.</summary>
    [Fact]
    public async Task RunAsyncFallsBackToPrUrlWhenThreadHasNoComments()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadEvent evt = JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"resolved",
                "thread":{"node_id":"RT_node_abc","comments":[]},
                "pull_request":{{TestFixtures.SimplePrJson(12, "Feature branch", "https://github.com/test/repo/pull/12", "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer", 60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", evt);

        await action.RunAsync();

        DiscordEmbed embed = captured!.Embeds![0];
        Assert.DoesNotContain(embed.Fields!, f => f.Name == "File");
        Assert.Null(embed.Description);
        Assert.Equal("https://github.com/test/repo/pull/12", embed.Url!.ToString());
    }

    /// <summary>The cache key contains the thread's NodeId.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-thread-12-RT_node_abc"), Times.Once);
    }

    /// <summary>
    /// Even for an unexpected payload with no thread field, it does not throw and
    /// falls back to "unknown" to send the notification
    /// (regression test for the old implementation where Octokit's Review property was always null).
    /// </summary>
    [Fact]
    public async Task RunAsyncFallsBackToUnknownWhenThreadFieldIsMissing()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadEvent eventWithoutThread = JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"resolved",
                "pull_request":{{TestFixtures.SimplePrJson(
                    12, "Feature branch",
                    "https://github.com/test/repo/pull/12",
                    "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", eventWithoutThread);

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Once);
        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-thread-12-unknown"), Times.Once);
    }

    /// <summary>
    /// Even when thread.node_id is a non-string JSON value (e.g. a number), it does not throw and
    /// falls back to "unknown" to send the notification (regression test for the exception caused by
    /// calling GetString() without checking ValueKind).
    /// </summary>
    [Fact]
    public async Task RunAsyncFallsBackToUnknownWhenThreadNodeIdIsNotAString()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadEvent eventWithNonStringNodeId = JsonSerializer.Deserialize<PullRequestReviewThreadEvent>(
            $$"""
            {
                "action":"resolved",
                "thread":{"node_id":12345,"comments":[{{TestFixtures.ReviewCommentJson()}}]},
                "pull_request":{{TestFixtures.SimplePrJson(
                    12, "Feature branch",
                    "https://github.com/test/repo/pull/12",
                    "pr-author", 50)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer",60)}}
            }
            """,
            OctokitJsonOptions.Value)!;

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", eventWithNonStringNodeId);

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Once);
        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-thread-12-unknown"), Times.Once);
    }

    /// <summary>When the PR author is mapped to Discord, the message is sent with a mention.</summary>
    [Fact]
    public async Task RunAsyncMentionsPrAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(50L)).Returns("discord-author-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestReviewThreadAction>>(),
            _webhookUri, "pull_request_review_thread", MakeEvent("resolved"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-author-id>"))),
            Times.Once);
    }
}
