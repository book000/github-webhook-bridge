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

public class SecretScanningAlertActionTests
{
    private const string FakeSecret = "FAKE-SECRET-VALUE-0000";

    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient> Discord, Mock<IMessageCacheService> Cache, Mock<IGitHubUserMapManager> UserMap) CreateMocks()
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

    private static SecretScanningAlertEvent MakeEvent(
        string action,
        string secretType = "github_personal_access_token",
        long number = 7,
        string? resolution = null,
        bool includeRepository = true,
        bool includeSender = true,
        string repoHtmlUrl = "https://github.com/test/repo")
    {
        var resolutionPart = resolution is null
            ? "\"resolution\":null,\"resolved_by\":null,\"resolved_at\":null"
            : $"\"resolution\":\"{resolution}\",\"resolved_by\":{TestFixtures.UserJson("resolver", 2)},\"resolved_at\":\"2024-01-02T00:00:00Z\"";
        var alert = $$"""{"number":{{number}},"secret_type":"{{secretType}}",{{resolutionPart}},"secret":"{{FakeSecret}}"}""";
        var repoPart = includeRepository
            ? $$""","repository":{{TestFixtures.RepoJson("test/repo", repoHtmlUrl)}}"""
            : string.Empty;
        var senderPart = includeSender
            ? $$""","sender":{{TestFixtures.UserJson("reporter", 1)}}"""
            : string.Empty;
        return JsonSerializer.Deserialize<SecretScanningAlertEvent>(
            $$"""{"action":"{{action}}","alert":{{alert}}{{repoPart}}{{senderPart}}}""",
            OctokitJsonOptions.Value)!;
    }

    private static (SecretScanningAlertAction Action, Mock<IDiscordClient> Discord, Mock<IMessageCacheService> Cache, List<string> Logs) CreateAction(
        SecretScanningAlertEvent evt)
    {
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap) = CreateMocks();

        List<string> logs = [];
        Mock<ILogger<SecretScanningAlertAction>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i =>
            {
                var formatter = Assert.IsAssignableFrom<Delegate>(i.Arguments[4]);
                logs.Add(Assert.IsType<string>(formatter.DynamicInvoke(i.Arguments[2], i.Arguments[3])));
            }));

        SecretScanningAlertAction action = new(
            discord.Object, cache.Object, userMap.Object, logger.Object,
            _webhookUri, "secret_scanning_alert", evt);
        return (action, discord, cache, logs);
    }

    private static DiscordEmbed SingleEmbed(Mock<IDiscordClient> discord)
    {
        DiscordMessage message = SentMessage(discord);
        return Assert.Single(message.Embeds!);
    }

    private static DiscordMessage SentMessage(Mock<IDiscordClient> discord)
        => Assert.IsType<DiscordMessage>(Assert.Single(discord.Invocations).Arguments[1]);

    private static string FieldValue(DiscordEmbed embed, string name)
        => Assert.Single(embed.Fields!, f => f.Name == name).Value;

    [Theory]
    [InlineData("created", "created", "open", EmbedColors.SecretScanningAlertOpen)]
    [InlineData("publicly_leaked", "publicly leaked", "open", EmbedColors.SecretScanningAlertOpen)]
    [InlineData("reopened", "reopened", "open", EmbedColors.SecretScanningAlertReopened)]
    [InlineData("validated", "validated", "open", EmbedColors.SecretScanningAlertOpen)]
    public async Task RunAsyncOpenActionsSendEmbedWithDerivedState(string action, string verb, string state, int color)
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) = CreateAction(MakeEvent(action));

        await act.RunAsync();

        DiscordEmbed embed = SingleEmbed(discord);
        Assert.Equal($"Secret scanning alert {verb}: #7 (github_personal_access_token) in test/repo", embed.Title);
        Assert.Equal(color, embed.Color);
        Assert.Equal(new Uri("https://github.com/test/repo/security/secret-scanning/7"), embed.Url);
        Assert.Equal("reporter", embed.Author!.Name);
        Assert.Null(embed.Description);
        Assert.Equal("test/repo", FieldValue(embed, "Repository"));
        Assert.Equal("#7", FieldValue(embed, "Alert"));
        Assert.Equal("github_personal_access_token", FieldValue(embed, "Secret type"));
        Assert.Equal(state, FieldValue(embed, "State"));
        Assert.DoesNotContain(embed.Fields!, f => f.Name == "Resolution");
    }

    [Theory]
    [InlineData("resolved", "resolved", "false_positive")]
    [InlineData("revoked", "revoked", "revoked")]
    public async Task RunAsyncResolvedActionsShowResolutionAndResolvedState(string action, string verb, string resolution)
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) =
            CreateAction(MakeEvent(action, resolution: resolution));

        await act.RunAsync();

        DiscordEmbed embed = SingleEmbed(discord);
        Assert.Equal($"Secret scanning alert {verb}: #7 (github_personal_access_token) in test/repo", embed.Title);
        Assert.Equal(EmbedColors.SecretScanningAlertResolved, embed.Color);
        Assert.Equal("resolved", FieldValue(embed, "State"));
        Assert.Equal(resolution, FieldValue(embed, "Resolution"));
    }

    [Theory]
    [InlineData("assigned")]
    [InlineData("unassigned")]
    public async Task RunAsyncSkippedActionsSendNothing(string action)
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, List<string> logs) = CreateAction(MakeEvent(action));

        await act.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
        Assert.Equal([$"Skipping secret_scanning_alert action {action} for test/repo."], logs);
    }

    [Fact]
    public async Task RunAsyncMissingRepositorySendsNothing()
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) =
            CreateAction(MakeEvent("created", includeRepository: false));

        await act.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsyncMissingAlertOrSecretTypeSendsNothingAndWarns(bool dropAlert)
    {
        SecretScanningAlertEvent valid = MakeEvent("created");
        SecretScanningAlertEvent evt = dropAlert
            ? valid with { Alert = null! }
            : valid with { Alert = valid.Alert with { SecretType = null! } };
        (Mock<IDiscordClient> discord, Mock<IMessageCacheService> cache, Mock<IGitHubUserMapManager> userMap) = CreateMocks();
        List<(LogLevel Level, string Message)> logs = [];
        SecretScanningAlertAction act = new(
            discord.Object, cache.Object, userMap.Object,
            TestFixtures.CaptureLogger<SecretScanningAlertAction>(logs).Object,
            _webhookUri, "secret_scanning_alert", evt);

        await act.RunAsync();

        TestFixtures.AssertSkippedWithWarning(
            discord, logs, "secret_scanning_alert payload is missing alert or alert.secret_type; skipping notification.");
    }

    [Fact]
    public async Task RunAsyncUnsupportedActionWithMissingAlertOnlyLogsTheSkip()
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, List<string> logs) =
            CreateAction(MakeEvent("assigned") with { Alert = null! });

        await act.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
        Assert.Equal(["Skipping secret_scanning_alert action assigned for test/repo."], logs);
    }

    [Fact]
    public async Task RunAsyncMissingSenderSendsEmbedWithoutAuthor()
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) =
            CreateAction(MakeEvent("created", includeSender: false));

        await act.RunAsync();

        DiscordEmbed embed = SingleEmbed(discord);
        Assert.Null(embed.Author);
        Assert.Equal("Secret scanning alert created: #7 (github_personal_access_token) in test/repo", embed.Title);
    }

    [Fact]
    public async Task RunAsyncNonAbsoluteRepositoryUrlOmitsLink()
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) =
            CreateAction(MakeEvent("created", repoHtmlUrl: "not-a-url"));

        await act.RunAsync();

        Assert.Null(SingleEmbed(discord).Url);
    }

    [Fact]
    public async Task RunAsyncLongSecretTypeTruncatesTitleTo256Characters()
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, _) =
            CreateAction(MakeEvent("created", secretType: new string('a', 2000)));

        await act.RunAsync();

        DiscordEmbed embed = SingleEmbed(discord);
        Assert.Equal(256, embed.Title?.Length);
        Assert.Equal(1024, FieldValue(embed, "Secret type").Length);
    }

    [Theory]
    [InlineData("created", null)]
    [InlineData("publicly_leaked", null)]
    [InlineData("resolved", "false_positive")]
    [InlineData("revoked", "revoked")]
    public async Task RunAsyncNeverExposesSecretInMessageOrLogs(string action, string? resolution)
    {
        (SecretScanningAlertAction act, Mock<IDiscordClient> discord, _, List<string> logs) =
            CreateAction(MakeEvent(action, resolution: resolution));

        await act.RunAsync();

        DiscordMessage sent = SentMessage(discord);
        Assert.DoesNotContain(FakeSecret, JsonSerializer.Serialize(sent), StringComparison.Ordinal);
        Assert.DoesNotContain(logs, l => l.Contains(FakeSecret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsyncUsesPerAlertCacheKey()
    {
        (SecretScanningAlertAction act, _, Mock<IMessageCacheService> cache, _) = CreateAction(MakeEvent("created", number: 42));

        await act.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-secret-scanning-alert-42"), Times.Once);
        cache.Verify(c => c.SetAsync(_webhookUri, "test/repo-secret-scanning-alert-42", "msg-id"), Times.Once);
    }
}
