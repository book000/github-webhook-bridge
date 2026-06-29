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

/// <summary>IssueCommentAction の通知内容・Issue/PR 判定・メンション・キャッシュキーテスト。</summary>
public class IssueCommentActionTests
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

    private static IssueCommentEvent MakeEvent(
        string action = "created",
        bool isPullRequest = false,
        string commentBody = "LGTM",
        long commentId = 9001) => JsonSerializer.Deserialize<IssueCommentEvent>(
        $$"""{"action":"{{action}}","issue":{{TestFixtures.IssueJson(3,"Test issue",isPr:isPullRequest,userLogin:"issue-author",userId:10)}},"comment":{{TestFixtures.IssueCommentJson(commentId,commentBody)}},"repository":{{TestFixtures.RepoJson("test/repo")}},"sender":{{TestFixtures.UserJson("commenter",20)}}}""",
        OctokitJsonOptions.Value)!;

    /// <summary>created + 通常 Issue のタイトルに "Issue" と "commented on" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedOnIssueTitleContainsIssue()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent("created", isPullRequest: false));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("Issue") &&
                    m.Embeds![0].Title!.Contains("commented on"))),
            Times.Once);
    }

    /// <summary>created + PR コメントのタイトルに "PR" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedOnPullRequestTitleContainsPr()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent("created", isPullRequest: true));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("PR"))),
            Times.Once);
    }

    /// <summary>created イベントは IssueCommentCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesIssueCommentCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent("created"));

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueCommentCreated, capturedColor);
    }

    /// <summary>キャッシュキーにコメント ID が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsCommentId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent(commentId: 9001));

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-issue-comment-9001"), Times.Once);
    }

    /// <summary>コメント本文が 500 文字超の場合は切り詰められる。</summary>
    [Fact]
    public async Task RunAsyncBodyTruncatedAt500Chars()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent(commentBody: new string('x', 600)));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    /// <summary>Issue 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsIssueAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // Issue 作成者 (id=10) が Discord にマッピングされている
        userMap.Setup(u => u.GetById(10L)).Returns("discord-id-of-author");

        IssueCommentAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<IssueCommentAction>>(),
            _webhookUri, "issue_comment", MakeEvent("created"));

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-id-of-author>"))),
            Times.Once);
    }
}
