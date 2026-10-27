using System.Text.Json;
using GitHubWebhookBridge.Functions;
using GitHubWebhookBridge.Services;

namespace GitHubWebhookBridge.Tests;

/// <summary>Guards the host and infrastructure settings that the queue worker's retry and serialization semantics depend on.</summary>
public class HostConfigurationTests
{
    private static string RepoFile(params string[] relative)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GitHubWebhookBridge.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine([dir.FullName, .. relative]);
    }

    [Fact]
    public void HostJson_QueueSettingsMatchTheWorkerContract()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(RepoFile("src", "host.json")));
        JsonElement queues = doc.RootElement.GetProperty("extensions").GetProperty("queues");

        Assert.Equal("base64", queues.GetProperty("messageEncoding").GetString());
        Assert.Equal(WebhookEventProcessor.MaxDequeueCount, queues.GetProperty("maxDequeueCount").GetInt32());
        Assert.Equal("00:00:30", queues.GetProperty("visibilityTimeout").GetString());
        Assert.Equal(1, queues.GetProperty("batchSize").GetInt32());
        Assert.Equal(0, queues.GetProperty("newBatchThreshold").GetInt32());
    }

    [Fact]
    public void HostJson_KeepsTheEmptyHttpRoutePrefix()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(RepoFile("src", "host.json")));

        Assert.Equal(string.Empty, doc.RootElement.GetProperty("extensions").GetProperty("http").GetProperty("routePrefix").GetString());
    }

    [Fact]
    public void Bicep_ProvisionsTheQueueResourcesAndCapsScaleOut()
    {
        var bicep = File.ReadAllText(RepoFile("infra", "main.bicep"));

        Assert.Contains($"name: '{QueueNames.Events}'", bicep, StringComparison.Ordinal);
        Assert.Contains($"name: '{QueueNames.Poison}'", bicep, StringComparison.Ordinal);
        Assert.Contains($"name: '{QueueNames.PayloadContainer}'", bicep, StringComparison.Ordinal);
        Assert.Contains($"prefixMatch: [{Environment.NewLine}                '{QueueNames.PayloadContainer}/'", bicep.ReplaceLineEndings(), StringComparison.Ordinal);
        Assert.Contains("daysAfterCreationGreaterThan: 7", bicep, StringComparison.Ordinal);
        Assert.Contains($"name: 'WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT'{Environment.NewLine}          value: '1'", bicep.ReplaceLineEndings(), StringComparison.Ordinal);
    }
}
