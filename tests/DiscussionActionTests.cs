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

/// <summary>Tests for DiscussionAction notification content, body truncation, and cache keys.</summary>
public class DiscussionActionTests
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

    private static string AnswerJson(long id, string body) =>
        $$"""{"id":{{id}},"node_id":"DC_{{id}}","html_url":"https://github.com/test/repo/discussions/5#discussioncomment-{{id}}","parent_id":null,"child_comment_count":0,"repository_url":"","discussion_id":5,"author_association":"NONE","user":{{TestFixtures.UserJson("answerer",2)}},"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","body":"{{body}}"}""";

    private static string CategoryJson(string name) =>
        $$"""{"id":1,"repository_id":1,"emoji":":speech_balloon:","name":"{{name}}","description":"","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","slug":"{{name.ToLowerInvariant().Replace(" ", "-")}}","is_answerable":false}""";

    private static DiscussionEvent MakeEvent(
        string action,
        string? labelName = null,
        string? answerBody = null,
        string? changedFromCategory = null) =>
        JsonSerializer.Deserialize<DiscussionEvent>(
            $$"""
            {
                "action":"{{action}}",
                "discussion":{{TestFixtures.DiscussionJson(5,"Great discussion","https://github.com/test/repo/discussions/5","body","author",1)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("author",1)}}
                {{(labelName is not null ? $",\"label\":{TestFixtures.LabelJson(labelName)}" : string.Empty)}}
                {{(answerBody is not null ? $",\"answer\":{AnswerJson(999, answerBody)}" : string.Empty)}}
                {{(changedFromCategory is not null ? $",\"changes\":{{\"category\":{{\"from\":{CategoryJson(changedFromCategory)}}}}}" : string.Empty)}}
                {{(action == "edited" ? ",\"changes\":{}" : string.Empty)}}
                {{(action == "unanswered" ? $",\"old_answer\":{AnswerJson(999, "a")}" : string.Empty)}}
            }
            """,
            OctokitJsonOptions.Value)!;

    /// <summary>The title of a created event contains "created" and the discussion number.</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCreatedAndNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion", MakeEvent("created"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("created") &&
                    m.Embeds![0].Title!.Contains("#5"))),
            Times.Once);
    }

    /// <summary>created uses the DiscussionCreated color.</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesDiscussionCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        DiscussionAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion", MakeEvent("created"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.DiscussionCreated, capturedColor);
    }

    /// <summary>The answered event uses answer.Body, not discussion.Body, for the description.</summary>
    [Fact]
    public async Task RunAsyncAnsweredUsesAnswerBodyNotDiscussionBody()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion",
            MakeEvent("answered", answerBody: "This is the answer"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("This is the answer") &&
                    !m.Embeds![0].Description!.Contains("original body"))),
            Times.Once);
    }

    /// <summary>The cache key contains the discussion number and the action suffix.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsDiscussionNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion", MakeEvent("created"));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-discussion-5-created"), Times.Once);
    }

    private static async Task<string> RunAndCaptureKeyAsync(string action)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        string? captured = null;
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .Callback<Uri, string>((_, key) => captured = key)
             .ReturnsAsync((CachedMessage?)null);

        DiscussionAction discussionAction = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion",
            MakeEvent(action, labelName: "bug", answerBody: "a"));

        await discussionAction.RunAsync();

        return captured!;
    }

    /// <summary>Actions that carry different content use different cache keys.</summary>
    [Theory]
    [InlineData("created", "edited")]
    [InlineData("created", "labeled")]
    [InlineData("created", "pinned")]
    [InlineData("created", "locked")]
    public async Task RunAsyncUnrelatedActionsUseDifferentCacheKeys(string first, string second)
    {
        var firstKey = await RunAndCaptureKeyAsync(first);
        var secondKey = await RunAndCaptureKeyAsync(second);

        Assert.NotEqual(firstKey, secondKey);
    }

    /// <summary>Paired actions share one cache key and its exact value.</summary>
    [Theory]
    [InlineData("pinned", "unpinned", "test/repo-discussion-5-pinned")]
    [InlineData("locked", "unlocked", "test/repo-discussion-5-locked")]
    [InlineData("answered", "unanswered", "test/repo-discussion-5-answered")]
    [InlineData("labeled", "unlabeled", "test/repo-discussion-5-label")]
    public async Task RunAsyncPairedActionsShareCacheKey(string first, string second, string expectedKey)
    {
        Assert.Equal(expectedKey, await RunAndCaptureKeyAsync(first));
        Assert.Equal(expectedKey, await RunAndCaptureKeyAsync(second));
    }

    /// <summary>The category_changed event includes the previous category name in a field (Changes.Category.From).</summary>
    [Fact]
    public async Task RunAsyncCategoryChangedIncludesNewCategoryField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionAction>>(),
            _webhookUri, "discussion",
            MakeEvent("category_changed", changedFromCategory: "Q&A"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("Q&A")))),
            Times.Once);
    }
}
