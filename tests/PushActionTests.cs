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

    private static PushEvent MakePushEvent(
        string[] commitJsons = null!,
        string refName = "refs/heads/main",
        bool created = false,
        bool deleted = false,
        bool forced = false,
        string after = "bbb")
    {
        var commitsJson = string.Join(",", commitJsons ?? [MakeCommitJson("abcdef1234567890", "feat: add new feature")]);
        var repoJson = TestFixtures.RepoJson("test/repo", "https://github.com/test/repo");
        var senderJson = TestFixtures.UserJson("octocat", 1, "https://github.com/octocat");
        var flags = $"\"created\":{created.ToString().ToLowerInvariant()},\"deleted\":{deleted.ToString().ToLowerInvariant()},\"forced\":{forced.ToString().ToLowerInvariant()}";
        return JsonSerializer.Deserialize<PushEvent>(
            $$$"""{"ref":"{{{refName}}}",{{{flags}}},"before":"aaa","after":"{{{after}}}","compare":"https://github.com","commits":[{{{commitsJson}}}],"repository":{{{repoJson}}},"sender":{{{senderJson}}},"pusher":{"name":"octocat","email":"octocat@example.com"}}""",
            OctokitJsonOptions.Value)!;
    }

    private static async Task<(Mock<IDiscordClient> Discord, Mock<IMessageCacheService> Cache)> RunAsync(PushEvent push)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", push);
        await action.RunAsync();
        return (discord, cache);
    }

    private static void VerifySentTitle(Mock<IDiscordClient> discord, string title) =>
        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds!.Count == 1 && m.Embeds[0].Title == title)),
            Times.Once);

    private static void VerifyCacheKey(Mock<IMessageCacheService> cache, string key) =>
        cache.Verify(c => c.GetAsync(It.IsAny<Uri>(), key), Times.Once);

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
    public async Task RunAsyncCacheKeyContainsRefAndAfterSha()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PushAction>>(),
            _webhookUri, "push", MakePushEvent());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo:refs/heads/main:bbb"), Times.Once);
    }

    [Fact]
    public async Task RunAsyncSeparatePushesToSameRefUseDifferentCacheKeys()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        foreach (var after in new[] { "bbb", "ccc" })
        {
            PushAction action = new(
                discord.Object, cache.Object, userMap.Object,
                Mock.Of<ILogger<PushAction>>(),
                _webhookUri, "push", MakePushEvent(after: after));

            await action.RunAsync();
        }

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo:refs/heads/main:bbb"), Times.Once);
        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo:refs/heads/main:ccc"), Times.Once);
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

    /// <summary>A payload missing the commits or the ref is skipped with a warning instead of throwing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsyncMissingCommitsOrRefSendsNothingAndWarns(bool dropCommits)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        List<(LogLevel Level, string Message)> logs = [];
        PushEvent evt = dropCommits
            ? MakePushEvent() with { Commits = null! }
            : MakePushEvent() with { Ref = null! };

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            TestFixtures.CaptureLogger<PushAction>(logs).Object,
            _webhookUri, "push", evt);

        await action.RunAsync();

        TestFixtures.AssertSkippedWithWarning(
            discord, logs, "push payload is missing commits or ref; skipping notification.");
    }

    /// <summary>An empty commit list is still skipped silently.</summary>
    [Fact]
    public async Task RunAsyncEmptyCommitsSendsNothingAndLogsNothing()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        List<(LogLevel Level, string Message)> logs = [];

        PushAction action = new(
            discord.Object, cache.Object, userMap.Object,
            TestFixtures.CaptureLogger<PushAction>(logs).Object,
            _webhookUri, "push", MakePushEvent() with { Commits = [] });

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
        Assert.Empty(logs);
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

    [Fact]
    public async Task RunAsyncNotifiesBranchDeletion()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent([], deleted: true));

        VerifySentTitle(discord, "[test/repo:main] branch deleted");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb:deleted");
    }

    [Fact]
    public async Task RunAsyncNotifiesTagDeletionWithoutRefsPrefix()
    {
        (Mock<IDiscordClient> discord, _) =
            await RunAsync(MakePushEvent([], refName: "refs/tags/v1", deleted: true));

        VerifySentTitle(discord, "[test/repo:v1] tag deleted");
    }

    [Fact]
    public async Task RunAsyncNotifiesBranchCreationWithoutCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent([], created: true));

        VerifySentTitle(discord, "[test/repo:main] branch created");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb:created");
    }

    [Fact]
    public async Task RunAsyncKeepsCommitFormatWhenBranchCreatedWithCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent(created: true));

        VerifySentTitle(discord, "[test/repo:main] 1 new commit(s)");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb");
    }

    [Fact]
    public async Task RunAsyncNotifiesForcePushWithoutCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent([], forced: true));

        VerifySentTitle(discord, "[test/repo:main] force pushed");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb:forced");
    }

    [Fact]
    public async Task RunAsyncMarksForcePushWithCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent(forced: true));

        VerifySentTitle(discord, "[test/repo:main] 1 new commit(s) (force push)");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb");
    }

    [Fact]
    public async Task RunAsyncNotifiesTagCreationWithoutCommits()
    {
        (Mock<IDiscordClient> discord, _) =
            await RunAsync(MakePushEvent([], refName: "refs/tags/v1", created: true));

        VerifySentTitle(discord, "[test/repo:v1] tag created");
    }

    [Fact]
    public async Task RunAsyncPrefersCreatedOverForcedWhenNoCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent([], created: true, forced: true));

        VerifySentTitle(discord, "[test/repo:main] branch created");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb:created");
    }

    [Fact]
    public async Task RunAsyncPrefersDeletedOverCommits()
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache) =
            await RunAsync(MakePushEvent(deleted: true));

        VerifySentTitle(discord, "[test/repo:main] branch deleted");
        VerifyCacheKey(cache, "test/repo:refs/heads/main:bbb:deleted");
    }

    [Fact]
    public async Task RunAsyncTreatsNonTagRefAsBranch()
    {
        (Mock<IDiscordClient> discord, _) =
            await RunAsync(MakePushEvent([], refName: "refs/notes/commits", deleted: true));

        VerifySentTitle(discord, "[test/repo:refs/notes/commits] branch deleted");
    }

    [Fact]
    public async Task RunAsyncDoesNotMarkNormalPushAsForcePush()
    {
        (Mock<IDiscordClient> discord, _) = await RunAsync(MakePushEvent());

        VerifySentTitle(discord, "[test/repo:main] 1 new commit(s)");
    }
}
