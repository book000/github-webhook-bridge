using GitHubWebhookBridge.Managers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class MuteManagerTests
{
    /// <summary>Helper that initializes MuteManager from a JSON string for testing.</summary>
    private static MuteManager CreateFromJson(string json)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
        config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
        config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
        var factory = new Mock<IHttpClientFactory>();
        var mgr = new MuteManager(config.Object, factory.Object);
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
}
