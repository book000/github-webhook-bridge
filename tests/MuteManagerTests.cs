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
        // push/a は除外 → ミュートされない
        Assert.False(mgr.IsMuted(1, "push", "a"));
        // issues はリストにない → ミュートされる
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
        // Include モード: actions リストが非 null だが action パラメータが null → ミュートされない
        // TypeScript 版 mute.ts line ~72 相当: if (action === null) return false
        MuteManager mgr = CreateFromJson(
            """[{"userId":1,"type":"include","events":[{"eventName":"issues","actions":["opened"]}]}]""");
        Assert.False(mgr.IsMuted(1, "issues", null));
    }
}
