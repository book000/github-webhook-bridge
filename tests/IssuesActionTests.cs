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

/// <summary>Tests for IssuesAction notification content, cache keys, and body truncation.</summary>
public class IssuesActionTests
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

    private static IssuesEvent MakeEvent(
        string action,
        string? labelName = null,
        string? assigneeLogin = null,
        string? milestoneTitle = null,
        string issueBody = "") => JsonSerializer.Deserialize<IssuesEvent>(
        $$"""{"action":"{{action}}","issue":{{TestFixtures.IssueJson(7,"Fix bug",action=="closed"?"closed":"open",body:issueBody.Length>0?issueBody:null,userLogin:"opener",userId:1)}},"repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},"sender":{{TestFixtures.UserJson("sender",2)}}{{(labelName is not null ? $",\"label\":{TestFixtures.LabelJson(labelName)}" : "")}}{{(assigneeLogin is not null ? $",\"assignee\":{TestFixtures.UserJson(assigneeLogin,3)}" : "")}}{{(milestoneTitle is not null ? $",\"milestone\":{TestFixtures.MilestoneJson(milestoneTitle)}" : "")}}}""",
        OctokitJsonOptions.Value)!;

    /// <summary>The opened event contains "opened" and the Issue number in the title.</summary>
    [Fact]
    public async Task RunAsyncOpenedTitleContainsOpenedAndNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("opened"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("opened") &&
                    m.Embeds![0].Title!.Contains("#7"))),
            Times.Once);
    }

    /// <summary>The opened event uses the IssueOpened color.</summary>
    [Fact]
    public async Task RunAsyncOpenedUsesIssueOpenedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("opened"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueOpened, capturedColor);
    }

    /// <summary>The closed event uses the IssueClosed color.</summary>
    [Fact]
    public async Task RunAsyncClosedUsesIssueClosedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("closed"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueClosed, capturedColor);
    }

    /// <summary>The labeled event includes the label name in an embed field.</summary>
    [Fact]
    public async Task RunAsyncLabeledIncludesLabelField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("labeled", labelName: "bug"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("bug")))),
            Times.Once);
    }

    /// <summary>labeled and unlabeled share the common cache key suffix "label".</summary>
    [Fact]
    public async Task RunAsyncLabeledAndUnlabeledShareCacheKeySuffix()
    {
        (Mock<IDiscordClient>? discord1, Mock<IMessageCacheService>? cache1, Mock<IGitHubUserMapManager>? userMap1) = CreateMocks();
        (Mock<IDiscordClient>? discord2, Mock<IMessageCacheService>? cache2, Mock<IGitHubUserMapManager>? userMap2) = CreateMocks();

        IssuesAction action1 = new(discord1.Object, cache1.Object, userMap1.Object, Mock.Of<ILogger<IssuesAction>>(), _webhookUri, "issues", MakeEvent("labeled", labelName: "bug"));
        IssuesAction action2 = new(discord2.Object, cache2.Object, userMap2.Object, Mock.Of<ILogger<IssuesAction>>(), _webhookUri, "issues", MakeEvent("unlabeled", labelName: "bug"));

        await action1.RunAsync();
        await action2.RunAsync();

        cache1.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-label"), Times.Once);
        cache2.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-label"), Times.Once);
    }

    /// <summary>The cache key for the opened event contains the Issue number.</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsIssueNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("opened"));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-opened"), Times.Once);
    }

    /// <summary>A body exceeding 500 characters is truncated and "..." is appended.</summary>
    [Fact]
    public async Task RunAsyncBodyTruncatedAt500Chars()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("opened", issueBody: new string('a', 600)));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    /// <summary>The milestoned event includes the milestone title in an embed field.</summary>
    [Fact]
    public async Task RunAsyncMilestonedIncludesMilestoneField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssuesAction>>(),
            _webhookUri, "issues", MakeEvent("milestoned", milestoneTitle: "v1.0"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("v1.0")))),
            Times.Once);
    }
}
