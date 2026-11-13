using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Tests;

public class PullRequestActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static string MakePrJson(
        string action,
        bool merged = false,
        bool draft = false,
        string? requestedReviewerLogin = null,
        long? requestedReviewerId = null,
        string title = "Add awesome feature",
        string body = "This PR adds an awesome feature.",
        string baseRef = "main",
        string? changedTitleFrom = null,
        string? changedBodyFrom = null,
        string? changesJsonOverride = null,
        string? requestedTeamName = null,
        string labelsJson = "[]",
        string assigneesJson = "[]",
        string requestedReviewersJson = "[]",
        string requestedTeamsJson = "[]")
    {
        var changeFields = new List<string>();
        if (changedTitleFrom is not null)
            changeFields.Add($"\"title\":{{\"from\":{JsonSerializer.Serialize(changedTitleFrom)}}}");
        if (changedBodyFrom is not null)
            changeFields.Add($"\"body\":{{\"from\":{JsonSerializer.Serialize(changedBodyFrom)}}}");
        if (changesJsonOverride is not null)
            changeFields.Add(changesJsonOverride);
        var changesJson = changeFields.Count > 0 ? ",\"changes\":{" + string.Join(",", changeFields) + "}" : string.Empty;

        return $$"""
            {
                "action":"{{action}}",
                "number":42,
                "pull_request":{{TestFixtures.PullRequestJson(
                    number: 42, title: JsonEscape(title),
                    htmlUrl: "https://github.com/test/repo/pull/42",
                    body: JsonEscape(body),
                    userLogin: "pr-author", userId: 100,
                    draft: draft, merged: merged,
                    headRef: "feature/my-branch", baseRef: baseRef,
                    labelsJson: labelsJson, assigneesJson: assigneesJson,
                    requestedReviewersJson: requestedReviewersJson,
                    requestedTeamsJson: requestedTeamsJson)}},
                "repository":{{TestFixtures.RepoJson("test/repo","https://github.com/test/repo")}},
                "sender":{{TestFixtures.UserJson("sender",200,"https://github.com/sender")}}
                {{(requestedReviewerLogin is not null
                    ? $",\"requested_reviewer\":{TestFixtures.UserJson(requestedReviewerLogin, requestedReviewerId ?? 300)}"
                    : string.Empty)}}
                {{changesJson}}
                {{(requestedTeamName is not null
                    ? $",\"requested_team\":{TestFixtures.TeamJson(requestedTeamName)}"
                    : string.Empty)}}
            }
            """;
    }

    /// <summary>Escapes text for embedding inside a JSON string literal produced by TestFixtures.</summary>
    private static string JsonEscape(string text) => JsonSerializer.Serialize(text)[1..^1];

    private static PullRequestEvent MakePrEvent(
        string action,
        bool merged = false,
        bool draft = false,
        string? requestedReviewerLogin = null,
        long? requestedReviewerId = null,
        string title = "Add awesome feature",
        string body = "This PR adds an awesome feature.",
        string? changedTitleFrom = null,
        string? changedBodyFrom = null,
        string? requestedTeamName = null,
        string labelsJson = "[]",
        string assigneesJson = "[]",
        string requestedReviewersJson = "[]",
        string requestedTeamsJson = "[]") =>
        JsonSerializer.Deserialize<PullRequestEvent>(
            MakePrJson(
                action, merged, draft, requestedReviewerLogin, requestedReviewerId,
                title: title, body: body,
                changedTitleFrom: changedTitleFrom, changedBodyFrom: changedBodyFrom,
                requestedTeamName: requestedTeamName, labelsJson: labelsJson,
                assigneesJson: assigneesJson, requestedReviewersJson: requestedReviewersJson,
                requestedTeamsJson: requestedTeamsJson),
            OctokitJsonOptions.Value)!;

    private static async Task<DiscordEmbed> RunAndCaptureEmbedAsync(IAction action, Mock<IDiscordClient> discord)
    {
        DiscordEmbed? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg.Embeds![0])
               .ReturnsAsync("msg-id");

        await action.RunAsync();

        return captured!;
    }

    private static async Task<DiscordEmbed> RunEditedAsync(PullRequestEvent prEvent)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", prEvent);

        return await RunAndCaptureEmbedAsync(action, discord);
    }

    private static async Task<DiscordEmbed> RunThroughFactoryAsync(string rawJson)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(discord.Object);
        services.AddSingleton(cache.Object);
        services.AddSingleton(userMap.Object);
        var factory = new ActionFactory(services.BuildServiceProvider());

        IAction action = factory.GetAction("pull_request", rawJson, _webhookUri);

        return await RunAndCaptureEmbedAsync(action, discord);
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

    private static async Task<DiscordMessage> RunAndCaptureMessageAsync(PullRequestEvent prEvent)
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscordMessage? captured = null;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _webhookUri, "pull_request", prEvent);

        await action.RunAsync();

        return captured!;
    }

    private static IReadOnlyList<DiscordEmbedField> FieldsOf(DiscordMessage message) => [.. message.Embeds![0].Fields ?? []];

    private static readonly string _twoLabelsJson = $"[{TestFixtures.LabelJson("bug")},{TestFixtures.LabelJson("docs")}]";

    private static readonly string _twoAssigneesJson = $"[{TestFixtures.UserJson("alice", 11)},{TestFixtures.UserJson("bob", 12)}]";

    private static readonly string _reviewersJson = $"[{TestFixtures.UserJson("carol", 13)}]";

    private static readonly string _teamsJson = $"[{TestFixtures.TeamJson("core-team", 21)},{TestFixtures.TeamJson("infra", 22)}]";

    /// <summary>A team review request shows the team name and mentions nobody.</summary>
    [Fact]
    public async Task RunAsyncReviewRequestedWithTeamShowsRequestedTeamField()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(
            MakePrEvent("review_requested", requestedTeamName: "core-team"));

        Assert.Equal("core-team", Assert.Single(FieldsOf(message), f => f.Name == "Requested Team").Value);
        Assert.Null(message.Content);
    }

    /// <summary>A removed team review request shows the team name.</summary>
    [Fact]
    public async Task RunAsyncReviewRequestRemovedWithTeamShowsRequestedTeamField()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(
            MakePrEvent("review_request_removed", requestedTeamName: "core-team"));

        Assert.Equal("core-team", Assert.Single(FieldsOf(message), f => f.Name == "Requested Team").Value);
    }

    /// <summary>A removed user review request shows the reviewer, as the requested event does.</summary>
    [Fact]
    public async Task RunAsyncReviewRequestRemovedWithReviewerShowsRequestedReviewerField()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(
            MakePrEvent("review_request_removed", requestedReviewerLogin: "reviewer-user", requestedReviewerId: 300));

        Assert.Equal("reviewer-user", Assert.Single(FieldsOf(message), f => f.Name == "Requested Reviewer").Value);
    }

    /// <summary>The opened event renders labels, assignees, requested reviewers and requested teams.</summary>
    [Fact]
    public async Task RunAsyncOpenedWithListsIncludesListFields()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(MakePrEvent(
            "opened",
            labelsJson: _twoLabelsJson,
            assigneesJson: _twoAssigneesJson,
            requestedReviewersJson: _reviewersJson,
            requestedTeamsJson: _teamsJson));

        IReadOnlyList<DiscordEmbedField> fields = FieldsOf(message);
        Assert.Equal("bug, docs", Assert.Single(fields, f => f.Name == "Labels").Value);
        Assert.Equal("alice, bob", Assert.Single(fields, f => f.Name == "Assignees").Value);
        Assert.Equal("carol", Assert.Single(fields, f => f.Name == "Requested Reviewers").Value);
        Assert.Equal("core-team, infra", Assert.Single(fields, f => f.Name == "Requested Teams").Value);
    }

    /// <summary>The opened event adds no list fields when the lists are empty.</summary>
    [Fact]
    public async Task RunAsyncOpenedWithoutListsOmitsListFields()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(MakePrEvent("opened"));

        Assert.DoesNotContain(FieldsOf(message), f => f.Name is "Labels" or "Assignees" or "Requested Reviewers" or "Requested Teams");
    }

    /// <summary>Only the opened event renders the lists, even when the PR object carries them.</summary>
    [Fact]
    public async Task RunAsyncClosedWithListsOmitsListFields()
    {
        DiscordMessage message = await RunAndCaptureMessageAsync(MakePrEvent(
            "closed",
            labelsJson: _twoLabelsJson,
            assigneesJson: _twoAssigneesJson,
            requestedReviewersJson: _reviewersJson,
            requestedTeamsJson: _teamsJson));

        Assert.DoesNotContain(FieldsOf(message), f => f.Name is "Labels" or "Assignees" or "Requested Reviewers" or "Requested Teams");
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

    /// <summary>An edited event with a body change shows a body diff.</summary>
    [Fact]
    public async Task RunAsyncEditedWithBodyChangeShowsBodyDiff()
    {
        DiscordEmbed embed = await RunEditedAsync(
            MakePrEvent("edited", body: "new body line", changedBodyFrom: "old body line"));

        Assert.StartsWith("```diff\n", embed.Description);
        Assert.Contains("--- body", embed.Description);
        Assert.Contains("- old body line", embed.Description);
        Assert.Contains("+ new body line", embed.Description);
    }

    /// <summary>When body and title both change, the body diff wins, as for issues.</summary>
    [Fact]
    public async Task RunAsyncEditedWithBodyAndTitleChangeShowsBodyDiff()
    {
        DiscordEmbed embed = await RunEditedAsync(
            MakePrEvent("edited", body: "new", changedBodyFrom: "old", changedTitleFrom: "Old title"));

        Assert.Contains("--- body", embed.Description);
        Assert.DoesNotContain("--- title", embed.Description);
    }

    /// <summary>An edited event with only a title change shows a title diff.</summary>
    [Fact]
    public async Task RunAsyncEditedWithTitleChangeShowsTitleDiff()
    {
        DiscordEmbed embed = await RunEditedAsync(MakePrEvent("edited", changedTitleFrom: "Old title"));

        Assert.StartsWith("```diff\n", embed.Description);
        Assert.Contains("--- title", embed.Description);
        Assert.Contains("- Old title", embed.Description);
        Assert.Contains("+ Add awesome feature", embed.Description);
    }

    /// <summary>A triple backtick in a title cannot close the diff code fence early.</summary>
    [Fact]
    public async Task RunAsyncEditedTitleWithBackticksStaysInsideFence()
    {
        DiscordEmbed embed = await RunEditedAsync(
            MakePrEvent("edited", title: "new ``` title", changedTitleFrom: "old ``` title"));

        var description = embed.Description!;
        Assert.StartsWith("```diff\n", description);
        Assert.EndsWith("```", description);
        var inner = description["```diff\n".Length..^3];
        Assert.DoesNotContain("```", inner, StringComparison.Ordinal);
    }

    /// <summary>A very long title diff is truncated to fit the Discord embed description limit.</summary>
    [Fact]
    public async Task RunAsyncEditedLongTitleDiffIsTruncated()
    {
        DiscordEmbed embed = await RunEditedAsync(
            MakePrEvent("edited", title: new string('n', 6000), changedTitleFrom: new string('o', 6000)));

        Assert.True(embed.Description!.Length <= 4096, $"Description length was {embed.Description.Length}");
    }

    /// <summary>A base branch change in the raw payload is shown as a Base changed field.</summary>
    [Fact]
    public async Task RunAsyncEditedWithBaseChangeShowsBaseChangedField()
    {
        DiscordEmbed embed = await RunThroughFactoryAsync(MakePrJson(
            "edited",
            baseRef: "develop",
            changesJsonOverride: """
                "base":{"ref":{"from":"main"},"sha":{"from":"x"}}
                """));

        DiscordEmbedField field = Assert.Single(embed.Fields!, f => f.Name == "Base changed");
        Assert.Equal("`main` → `develop`", field.Value);
    }

    /// <summary>An edited event without changes.base has no Base changed field.</summary>
    [Fact]
    public async Task RunAsyncEditedWithoutBaseChangeHasNoBaseChangedField()
    {
        DiscordEmbed embed = await RunThroughFactoryAsync(MakePrJson("edited", changedTitleFrom: "Old title"));

        Assert.DoesNotContain(embed.Fields!, f => f.Name == "Base changed");
    }

    /// <summary>A malformed changes.base shape never throws and yields no Base changed field.</summary>
    [Theory]
    [InlineData("""
        "base":null
        """)]
    [InlineData("""
        "base":{"ref":null}
        """)]
    [InlineData("""
        "base":{"ref":{"from":null}}
        """)]
    [InlineData("""
        "base":{"ref":"x"}
        """)]
    [InlineData("""
        "base":{"ref":{"from":1}}
        """)]
    [InlineData("""
        "base":"x"
        """)]
    [InlineData("""
        "base":{"ref":{"from":""}}
        """)]
    public async Task RunAsyncEditedWithMalformedBaseChangeHasNoBaseChangedField(string baseChangeJson)
    {
        DiscordEmbed embed = await RunThroughFactoryAsync(MakePrJson(
            "edited", changedTitleFrom: "Old title", changesJsonOverride: baseChangeJson));

        Assert.DoesNotContain(embed.Fields!, f => f.Name == "Base changed");
    }
}
