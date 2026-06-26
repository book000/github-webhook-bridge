using GitHubWebhookBridge.Managers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class MuteManagerTests
{
    /// <summary>テスト用に MuteManager を JSON 文字列から初期化するヘルパー。</summary>
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
    public void IsMuted_TypeAll_AlwaysTrue()
    {
        var mgr = CreateFromJson(
            """[{"userId":1,"type":"All","events":[]}]""");
        Assert.True(mgr.IsMuted(1, "push", null));
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Fact]
    public void IsMuted_TypeInclude_MatchingEvent_True()
    {
        var mgr = CreateFromJson(
            """[{"userId":1,"type":"Include","events":[{"eventName":"push","actions":null}]}]""");
        Assert.True(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public void IsMuted_TypeInclude_WithActions_OnlyMatchingAction_True()
    {
        var mgr = CreateFromJson(
            """[{"userId":1,"type":"Include","events":[{"eventName":"issues","actions":["opened"]}]}]""");
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
        Assert.False(mgr.IsMuted(1, "issues", "closed"));
    }

    [Fact]
    public void IsMuted_TypeExclude_NonExcludedEvent_True()
    {
        var mgr = CreateFromJson(
            """[{"userId":1,"type":"Exclude","events":[{"eventName":"push","actions":["a"]}]}]""");
        // push/a は除外 → ミュートされない
        Assert.False(mgr.IsMuted(1, "push", "a"));
        // issues はリストにない → ミュートされる
        Assert.True(mgr.IsMuted(1, "issues", "opened"));
    }

    [Fact]
    public void IsMuted_UnknownUser_False()
    {
        var mgr = CreateFromJson("""[]""");
        Assert.False(mgr.IsMuted(999, "push", null));
    }
}
