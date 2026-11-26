using System.Reflection;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="PurgeMessageCacheFunction"/>.</summary>
public class PurgeMessageCacheFunctionTests
{
    /// <summary>The purge is requested exactly once with the host token and the deleted count is logged.</summary>
    [Fact]
    public async Task RunAsyncPurgesOnceAndLogsTheDeletedCount()
    {
        using var cts = new CancellationTokenSource();
        var cache = new Mock<IMessageCacheService>();
        cache.Setup(c => c.DeleteExpiredAsync(cts.Token)).ReturnsAsync(3);
        var logger = new Mock<ILogger<PurgeMessageCacheFunction>>();
        logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);

        await new PurgeMessageCacheFunction(cache.Object, logger.Object).RunAsync(null!, cts.Token);

        cache.Verify(c => c.DeleteExpiredAsync(cts.Token), Times.Once);
        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => string.Equals(state!.ToString(), "Purged 3 expired message cache entities", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>The function is registered as PurgeMessageCache and runs daily at 18:00 UTC (03:00 JST) without running on startup.</summary>
    [Fact]
    public void RunAsyncIsADailyTimerFunction()
    {
        MethodInfo method = typeof(PurgeMessageCacheFunction).GetMethod(nameof(PurgeMessageCacheFunction.RunAsync))!;

        Assert.Equal("PurgeMessageCache", method.GetCustomAttribute<FunctionAttribute>()!.Name);
        TimerTriggerAttribute trigger = method.GetParameters()[0].GetCustomAttribute<TimerTriggerAttribute>()!;
        Assert.Equal("0 0 18 * * *", trigger.Schedule);
        Assert.False(trigger.RunOnStartup);
    }
}
