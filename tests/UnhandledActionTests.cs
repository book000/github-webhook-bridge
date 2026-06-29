using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

public class UnhandledActionTests
{
    [Fact]
    public async Task RunAsync_ThrowsNotImplementedException()
    {
        var action = new UnhandledAction("workflow_run");
        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    [Fact]
    public async Task RunAsync_ExceptionMessageContainsEventName()
    {
        var action = new UnhandledAction("some_event");
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
        Assert.Contains("some_event", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnhandledAction_HasNoGitHubEventAttribute()
    {
        var attr = typeof(UnhandledAction)
            .GetCustomAttributes(typeof(GitHubEventAttribute), inherit: false);
        Assert.Empty(attr);
    }
}
