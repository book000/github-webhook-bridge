using GitHubWebhookBridge.Actions;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Tests;

public class IgnoredActionTests
{
    private sealed class ListLogger : ILogger<IgnoredAction>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task RunAsync_LogsOneInformationEntryNamingTheEventAndAction()
    {
        var logger = new ListLogger();
        var action = new IgnoredAction(logger, "issues", "status_changed");

        await action.RunAsync();

        (LogLevel level, var text) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal("Ignored webhook event issues.status_changed: no notification is sent for this action", text);
    }

    [Fact]
    public void IgnoredAction_HasNoGitHubEventAttribute()
    {
        var attr = typeof(IgnoredAction)
            .GetCustomAttributes(typeof(GitHubEventAttribute), inherit: false);
        Assert.Empty(attr);
    }
}
