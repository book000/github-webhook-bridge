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

public class PushActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static string MakeCommitJson(string id, string message, string authorName = "octocat", string url = "https://github.com/test/repo/commit/abc") =>
        $$$"""{"id":"{{{id}}}","tree_id":"tree1","distinct":true,"message":"{{{message}}}","timestamp":"2024-01-01T00:00:00Z","url":"{{{url}}}","author":{"name":"{{{authorName}}}","email":"u@x.com","username":"{{{authorName}}}"},"committer":{"name":"{{{authorName}}}","email":"u@x.com","username":"{{{authorName}}}"},"added":[],"removed":[],"modified":[]}""";

    private static PushEvent MakePushEvent(string[] commitJsons = null!, string compare = "https://github.com")
    {
        var commitsJson = string.Join(",", commitJsons ?? [MakeCommitJson("abcdef1234567890", "feat: add new feature")]);
        var repoJson = TestFixtures.RepoJson("test/repo", "https://github.com/test/repo");
        var senderJson = TestFixtures.UserJson("octocat", 1, "https://github.com/octocat");
        return JsonSerializer.Deserialize<PushEvent>(
            $$$"""{"ref":"refs/heads/main","before":"aaa","after":"bbb","compare":"{{{compare}}}","commits":[{{{commitsJson}}}],"repository":{{{repoJson}}},"sender":{{{senderJson}}},"pusher":{"name":"octocat","email":"octocat@example.com"}}""",
            OctokitJsonOptions.Value)!;
    }

    private static async Task<DiscordEmbed> RunAndCaptureEmbedAsync(PushEvent pushEvent)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        DiscordEmbed? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, m) => captured = m.Embeds![0])
               .ReturnsAsync("msg-id");

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", pushEvent);

        await action.RunAsync();

        return captured!;
    }

    [Fact]
    public async Task RunAsyncLinksCompareUrlFromPayload()
    {
        DiscordEmbed embed = await RunAndCaptureEmbedAsync(
            MakePushEvent(compare: "https://github.com/test/repo/compare/aaa...bbb"));

        Assert.Equal(new Uri("https://github.com/test/repo/compare/aaa...bbb"), embed.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    public async Task RunAsyncOmitsEmbedUrlWhenCompareIsNotHttpUrl(string compare)
    {
        DiscordEmbed embed = await RunAndCaptureEmbedAsync(MakePushEvent(compare: compare));

        Assert.Null(embed.Url);
    }

    [Fact]
    public async Task RunAsyncCommitMessageTruncationDoesNotSplitSurrogatePair()
    {
        var message = new string('a', 49) + "😀" + "tail";

        DiscordEmbed embed = await RunAndCaptureEmbedAsync(
            MakePushEvent([MakeCommitJson("abc1234", message)]));

        Assert.Contains($" {new string('a', 49)}... - octocat", embed.Description);
    }

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

        return (discord, cache, userMap);
    }

    [Fact]
    public async Task RunAsyncSendsMessageWithCommitInfo()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", MakePushEvent());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds != null &&
                    m.Embeds.Count == 1 &&
                    m.Embeds[0].Title!.Contains("test/repo:main") &&
                    m.Embeds[0].Title!.Contains("1 new commit"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncDoesNotSendWhenNoCommits()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // Create a PushEvent with an empty commit list
        var repoJson = TestFixtures.RepoJson("test/repo", "https://github.com/test/repo");
        var senderJson = TestFixtures.UserJson();
        var emptyPush = JsonSerializer.Deserialize<PushEvent>(
            $$$"""{"ref":"refs/heads/main","before":"aaa","after":"bbb","compare":"https://github.com","commits":[],"repository":{{{repoJson}}},"sender":{{{senderJson}}},"pusher":{"name":"octocat","email":""}}""",
            OctokitJsonOptions.Value)!;

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", emptyPush);

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsyncStripsRefsHeadsPrefix()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var commitJson = MakeCommitJson("abc1", "test");
        var repoJson = TestFixtures.RepoJson("test/repo");
        var senderJson = TestFixtures.UserJson();
        var push = JsonSerializer.Deserialize<PushEvent>(
            $$$"""{"ref":"refs/heads/feature/my-branch","before":"aaa","after":"bbb","compare":"https://github.com","commits":[{{{commitJson}}}],"repository":{{{repoJson}}},"sender":{{{senderJson}}},"pusher":{"name":"octocat","email":""}}""",
            OctokitJsonOptions.Value)!;

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", push);

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("feature/my-branch"))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncTruncatesLongCommitMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        string longMessage = new('a', 60); // 60 chars > 50 char limit
        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push",
            MakePushEvent([MakeCommitJson("abc1234", longMessage)]));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    [Fact]
    public async Task RunAsyncOnlyUsesFirstLineOfMultiLineMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push",
            MakePushEvent([MakeCommitJson("abc1234", "first line\\nsecond line\\nthird")]));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("first line") &&
                    !m.Embeds![0].Description!.Contains("second line"))),
            Times.Once);
    }
}
