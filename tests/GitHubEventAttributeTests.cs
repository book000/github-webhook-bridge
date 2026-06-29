using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

public class GitHubEventAttributeTests
{
    [Fact]
    public void StoresEventName()
    {
        var attr = new GitHubEventAttribute("pull_request");
        Assert.Equal("pull_request", attr.EventName);
    }

    [Fact]
    public void Attribute_TargetsClassOnly()
    {
        var usage = typeof(GitHubEventAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.Inherited);
    }
}
