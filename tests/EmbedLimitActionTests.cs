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

/// <summary>Tests that real actions keep over-limit GitHub-controlled text within Discord's embed limits.</summary>
public class EmbedLimitActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient> Discord, Mock<IMessageCacheService> Cache, Mock<IGitHubUserMapManager> UserMap, List<DiscordEmbed> Sent) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();
        List<DiscordEmbed> sent = [];

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>())).ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, m) => sent.AddRange(m.Embeds!))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap, sent);
    }

    private static void AssertWithinLimits(DiscordEmbed embed)
    {
        Assert.True(embed.Title!.Length <= 256, $"title length {embed.Title.Length}");
        Assert.True((embed.Description?.Length ?? 0) <= 4096, "description");
        Assert.True((embed.Fields?.Count ?? 0) <= 25, "field count");
        Assert.All(embed.Fields ?? [], f =>
        {
            Assert.True(f.Name.Length <= 256, $"field name length {f.Name.Length}");
            Assert.True(f.Value.Length <= 1024, $"field value length {f.Value.Length}");
        });
        Assert.True((embed.Author?.Name.Length ?? 0) <= 256, "author name");

        var total = embed.Title.Length
            + (embed.Description?.Length ?? 0)
            + (embed.Footer?.Text.Length ?? 0)
            + (embed.Author?.Name.Length ?? 0)
            + (embed.Fields?.Sum(f => f.Name.Length + f.Value.Length) ?? 0);
        Assert.True(total <= 6000, $"total length {total}");
    }

    [Fact]
    public async Task IssuesAction_LongTitleAndMilestone_StaysWithinLimits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap, List<DiscordEmbed> sent) = CreateMocks();
        var longText = new string('x', 2000);
        IssuesEvent evt = JsonSerializer.Deserialize<IssuesEvent>(
            $$"""{"action":"milestoned","issue":{{TestFixtures.IssueJson(7, longText, "open", userLogin: "opener", userId: 1)}},"repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},"sender":{{TestFixtures.UserJson("sender", 2)}},"milestone":{{TestFixtures.MilestoneJson(longText)}}}""",
            OctokitJsonOptions.Value)!;

        await new IssuesAction(discord.Object, cache.Object, userMap.Object, Mock.Of<ILogger<IssuesAction>>(), _webhookUri, "issues", evt).RunAsync();

        DiscordEmbed embed = Assert.Single(sent);
        Assert.Equal(256, embed.Title!.Length);
        Assert.Equal(1024, embed.Fields!.Single(f => f.Name == "Milestone").Value.Length);
        AssertWithinLimits(embed);
    }

    [Fact]
    public async Task PullRequestReviewCommentAction_LongFilePath_StaysWithinLimits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap, List<DiscordEmbed> sent) = CreateMocks();
        var longPath = $"src/{new string('p', 2000)}.cs";
        PullRequestReviewCommentEvent evt = JsonSerializer.Deserialize<PullRequestReviewCommentEvent>(
            $$"""
            {
                "action":"created",
                "comment":{{TestFixtures.ReviewCommentJson(5001, "Looks good!", longPath, "https://github.com/test/repo/pull/10#discussion_r5001")}},
                "pull_request":{{TestFixtures.SimplePrJson(10, "My PR", "https://github.com/test/repo/pull/10", "pr-author", 20)}},
                "repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("reviewer", 30)}}
            }
            """,
            OctokitJsonOptions.Value)!;

        await new PullRequestReviewCommentAction(discord.Object, cache.Object, userMap.Object, Mock.Of<ILogger<PullRequestReviewCommentAction>>(), _webhookUri, "pull_request_review_comment", evt).RunAsync();

        DiscordEmbed embed = Assert.Single(sent);
        Assert.Equal(1024, embed.Fields!.Single(f => f.Name == "File").Value.Length);
        AssertWithinLimits(embed);
    }

    [Fact]
    public async Task PullRequestAction_LongTitle_StaysWithinLimits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap, List<DiscordEmbed> sent) = CreateMocks();
        PullRequestEvent evt = JsonSerializer.Deserialize<PullRequestEvent>(
            $$"""
            {
                "action":"opened",
                "number":42,
                "pull_request":{{TestFixtures.PullRequestJson(number: 42, title: new string('t', 2000), htmlUrl: "https://github.com/test/repo/pull/42", body: "body", userLogin: "pr-author", userId: 100)}},
                "repository":{{TestFixtures.RepoJson("test/repo", "https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("sender", 200, "https://github.com/sender")}}
            }
            """,
            OctokitJsonOptions.Value)!;

        await new PullRequestAction(discord.Object, cache.Object, userMap.Object, Mock.Of<ILogger<PullRequestAction>>(), _webhookUri, "pull_request", evt).RunAsync();

        DiscordEmbed embed = Assert.Single(sent);
        Assert.Equal(256, embed.Title!.Length);
        AssertWithinLimits(embed);
    }
}
