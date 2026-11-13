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

/// <summary>Tests for DiscussionCommentAction notification content, colors, body truncation, and cache keys.</summary>
public class DiscussionCommentActionTests
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

    private static string CommentJson(long id, string body) =>
        $$$"""{"id":{{{id}}},"node_id":"DC_{{{id}}}","html_url":"https://github.com/test/repo/discussions/5#discussioncomment-{{{id}}}","parent_id":null,"child_comment_count":0,"repository_url":"","discussion_id":5,"author_association":"NONE","user":{{{TestFixtures.UserJson("commenter", 20)}}},"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","body":"{{{body}}}","reactions":{"url":"","total_count":0,"+1":0,"-1":0,"laugh":0,"hooray":0,"confused":0,"heart":0,"rocket":0,"eyes":0}}""";

    private static DiscussionCommentEvent MakeEvent(
        string action,
        string commentBody = "A reply",
        long commentId = 9001) =>
        JsonSerializer.Deserialize<DiscussionCommentEvent>(
            $$"""{"action":"{{action}}","discussion":{{TestFixtures.DiscussionJson(5, "Great discussion", "https://github.com/test/repo/discussions/5", "body", "author", 10)}},"comment":{{CommentJson(commentId, commentBody)}},"repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},"sender":{{TestFixtures.UserJson("commenter", 20)}}{{(action == "edited" ? ""","changes":{"body":{"from":"before"}}""" : string.Empty)}}}""",
            OctokitJsonOptions.Value)!;

    private static async Task<DiscordMessage> RunAsync(DiscussionCommentEvent evt, Mock<IMessageCacheService>? cacheOverride = null)
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap) = CreateMocks();

        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        DiscussionCommentAction action = new(
            discord.Object, (cacheOverride ?? cache).Object, userMap.Object,
            Mock.Of<ILogger<DiscussionCommentAction>>(),
            _webhookUri, "discussion_comment", evt);

        await action.RunAsync();

        Assert.NotNull(captured);
        return captured!;
    }

    /// <summary>A payload missing the discussion or the comment object is skipped with a warning instead of throwing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsyncMissingDiscussionOrCommentSendsNothingAndWarns(bool dropDiscussion)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        List<(LogLevel Level, string Message)> logs = [];
        DiscussionCommentEvent evt = dropDiscussion
            ? MakeEvent("created") with { Discussion = null! }
            : MakeEvent("created") with { Comment = null! };

        DiscussionCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            TestFixtures.CaptureLogger<DiscussionCommentAction>(logs).Object,
            _webhookUri, "discussion_comment", evt);

        await action.RunAsync();

        TestFixtures.AssertSkippedWithWarning(
            discord, logs, "discussion_comment payload is missing discussion or comment; skipping notification.");
    }

    /// <summary>created shows the commenter, discussion number/title, comment body, comment URL, and the created color.</summary>
    [Fact]
    public async Task RunAsyncCreatedShowsCommentDetails()
    {
        DiscordMessage message = await RunAsync(MakeEvent("created", commentBody: "Thanks for the report"));

        DiscordEmbed embed = message.Embeds![0];
        Assert.Equal("commenter commented on Discussion #5: Great discussion", embed.Title);
        Assert.Equal("Thanks for the report", embed.Description);
        Assert.Equal(new Uri("https://github.com/test/repo/discussions/5#discussioncomment-9001"), embed.Url);
        Assert.Equal("commenter", embed.Author!.Name);
        Assert.Equal(EmbedColors.DiscussionCommentCreated, embed.Color);
    }

    /// <summary>edited shows the current body (no diff) with the edited verb and color.</summary>
    [Fact]
    public async Task RunAsyncEditedShowsCurrentBody()
    {
        DiscordMessage message = await RunAsync(MakeEvent("edited", commentBody: "LGTM"));

        DiscordEmbed embed = message.Embeds![0];
        Assert.Equal("commenter edited comment on Discussion #5: Great discussion", embed.Title);
        Assert.Equal("LGTM", embed.Description);
        Assert.Equal(EmbedColors.DiscussionCommentEdited, embed.Color);
    }

    /// <summary>deleted uses the deleted verb and color.</summary>
    [Fact]
    public async Task RunAsyncDeletedUsesDeletedVerbAndColor()
    {
        DiscordMessage message = await RunAsync(MakeEvent("deleted"));

        DiscordEmbed embed = message.Embeds![0];
        Assert.Equal("commenter deleted comment on Discussion #5: Great discussion", embed.Title);
        Assert.Equal(EmbedColors.DiscussionCommentDeleted, embed.Color);
    }

    /// <summary>A comment body longer than 500 characters is truncated with an ellipsis.</summary>
    [Fact]
    public async Task RunAsyncLongBodyIsTruncatedTo500Characters()
    {
        DiscordMessage message = await RunAsync(MakeEvent("created", commentBody: new string('x', 600)));

        Assert.Equal(new string('x', 500) + "...", message.Embeds![0].Description);
    }

    /// <summary>The discussion author is @mentioned when mapped and not the sender.</summary>
    [Fact]
    public async Task RunAsyncMentionsDiscussionAuthorWhenMapped()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap) = CreateMocks();
        userMap.Setup(u => u.GetById(10L)).Returns("123");
        userMap.Setup(u => u.GetById(20L)).Returns("456");

        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        DiscussionCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionCommentAction>>(),
            _webhookUri, "discussion_comment", MakeEvent("created"));

        await action.RunAsync();

        Assert.Equal("<@123>", captured!.Content);
    }

    /// <summary>The cache key is scoped to the comment ID, so an edit within the cache window edits the same message.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsCommentId()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap) = CreateMocks();

        DiscussionCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<DiscussionCommentAction>>(),
            _webhookUri, "discussion_comment", MakeEvent("created", commentId: 9001));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-discussion-comment-9001"), Times.Once);
    }
}
