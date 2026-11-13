using GitHubWebhookBridge.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class MuteManagerTests
{
    private sealed class CapturingLogger : ILogger<MuteManager>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>Helper that initializes MuteManager from a JSON string for testing.</summary>
    private static MuteManager CreateFromJson(string json, ILogger<MuteManager>? logger = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
        config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
        config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
        var factory = new Mock<IHttpClientFactory>();
        var mgr = new MuteManager(config.Object, factory.Object, null, logger);
        mgr.LoadForTest(json);
        return mgr;
    }

    [Fact]
    public void IsMutedTypeAllAlwaysTrue()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"all","events":[]}]""");
        Assert.True(mgr.IsMuted(1, "push", null));
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Fact]
    public void IsMutedTypeIncludeMatchingEventTrue()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":"push","actions":null}]}]""");
        Assert.True(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public void IsMutedTypeIncludeWithActionsOnlyMatchingActionTrue()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":"issues","actions":["opened"]}]}]""");
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
        Assert.False(mgr.IsMuted(1, "issues", "closed"));
    }

    [Fact]
    public void IsMutedTypeExcludeNonExcludedEventTrue()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"exclude","events":[{"eventName":"push","actions":["a"]}]}]""");
        // push/a is excluded -> not muted
        Assert.False(mgr.IsMuted(1, "push", "a"));
        // issues is not in the list -> muted
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Fact]
    public void IsMutedUnknownUserFalse()
    {
        MuteManager mgr = CreateFromJson("""[]""");
        Assert.False(mgr.IsMuted(999, "push", null));
    }

    [Fact]
    public void IsMutedTypeIncludeWithActionsListNullActionFalse()
    {
        // Include mode: the actions list is non-null but the action parameter is null -> not muted
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":"issues","actions":["opened"]}]}]""");
        Assert.False(mgr.IsMuted(1, "issues", null));
    }

    [Fact]
    public void IsMutedTypeExcludeWithOmittedActionsExemptsEveryActionOfThatEvent()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"exclude","events":[{"eventName":"push"}]}]""");
        Assert.False(mgr.IsMuted(1, "push", "x"));
        Assert.False(mgr.IsMuted(1, "push", null));
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Fact]
    public void IsMutedTypeIncludeMatchesCaseInsensitively()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":"Issues","actions":["Opened"]}]}]""");
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
        Assert.False(mgr.IsMuted(1, "issues", "closed"));
    }

    [Fact]
    public void IsMutedTypeExcludeMatchesCaseInsensitively()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"exclude","events":[{"eventName":"Push","actions":["Created"]}]}]""");
        Assert.False(mgr.IsMuted(1, "push", "created"));
        Assert.True(mgr.IsMuted(1, "push", "deleted"));
    }

    [Fact]
    public void IsMutedIgnoresWhitespaceAroundEventNamesAndActions()
    {
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":" issues ","actions":[" opened "]}]}]""");
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Theory]
    [InlineData("include", false)]
    [InlineData("exclude", true)]
    [InlineData("all", true)]
    public void IsMutedRecordWithoutEventsDoesNotThrowAndWarns(string type, bool expectedMuted)
    {
        CapturingLogger logger = new();
        MuteManager mgr = CreateFromJson($$"""[{"userId":7,"type":"{{type}}"}]""", logger);

        Assert.Equal(expectedMuted, mgr.IsMuted(7, "push", null));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("userId 7", StringComparison.Ordinal));
    }

    [Fact]
    public void IsMutedDuplicateUserIdFirstRecordWinsAndWarns()
    {
        CapturingLogger logger = new();
        MuteManager mgr = CreateFromJson(
            """
            [{"userId":5,"type":"include","events":[{"eventName":"push"}]},
             {"userId":5,"type":"all","events":[]}]
            """,
            logger);

        Assert.True(mgr.IsMuted(5, "push", null));
        Assert.False(mgr.IsMuted(5, "issues", "opened"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("userId 5", StringComparison.Ordinal));
    }

    [Fact]
    public void IsMutedNullEntriesDoNotThrow()
    {
        MuteManager mgr = CreateFromJson(
            """
            [null,
             {"userId":3,"type":"include","events":[null,{"eventName":null},{"eventName":"  "},{"eventName":"push","actions":[null,"  ","opened"]}]}]
            """);

        Assert.True(mgr.IsMuted(3, "push", "opened"));
        Assert.False(mgr.IsMuted(3, "push", "closed"));
        Assert.False(mgr.IsMuted(3, "issues", "opened"));
    }
}
