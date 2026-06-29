using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>ActionFactory のリフレクションレジストリ動作テスト。</summary>
public class ActionFactoryTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    /// <summary>テスト用 DI コンテナ。ActionFactory が必要とするサービスをモックで登録する。</summary>
    internal static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ActionFactory / ActionRegistryValidator が DI から解決するサービスをモックで登録する
        services.AddSingleton(Mock.Of<IDiscordClient>());
        services.AddSingleton(Mock.Of<IMessageCacheService>());
        services.AddSingleton(Mock.Of<IGitHubUserMapManager>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_BuildsRegistryWithoutThrowing()
    {
        Exception? ex = Record.Exception(() => new ActionFactory(BuildServiceProvider()));
        Assert.Null(ex);
    }

    [Fact]
    public void GetAction_UnknownEvent_ReturnsUnhandledAction()
    {
        var factory = new ActionFactory(BuildServiceProvider());
        var action = factory.GetAction("completely_unknown_event", "{}", _webhookUri);
        Assert.IsType<UnhandledAction>(action);
    }

    [Fact]
    public async Task GetAction_UnknownEvent_ThrowsNotImplementedException()
    {
        var factory = new ActionFactory(BuildServiceProvider());
        var action = factory.GetAction("unknown_event", "{}", _webhookUri);
        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    [Fact]
    public void ActionRegistryValidator_ValidateAll_DoesNotThrow()
    {
        var sp = BuildServiceProvider();
        var factory = new ActionFactory(sp);
        var validator = new ActionRegistryValidator(factory, sp);

        // Octokit 型は required メンバーを持つためペイロード生成をスキップするが例外は発生しない
        var ex = Record.Exception(() => validator.ValidateAll());
        Assert.Null(ex);
    }

    /// <summary>Task 8 完了: 12 アクションがすべて登録されていることを検証する。</summary>
    [Fact]
    public void Registry_ContainsTwelveActions()
    {
        var factory = new ActionFactory(BuildServiceProvider());

        Assert.Equal(12, factory.Registry.Count);
    }

    /// <summary>既知イベント名で GetAction を呼ぶと対応する具象型が返ることを検証する。</summary>
    public static IEnumerable<object[]> KnownEventData()
    {
        yield return
        [
            "ping",
            TestFixtures.PingEventJson(
                zen: "Non-blocking is better than blocking.",
                hookId: 1,
                hookType: "Repository",
                repoFullName: "owner/repo",
                senderLogin: "user"),
            typeof(PingAction)
        ];
        yield return
        [
            "push",
            $$$"""{"ref":"refs/heads/main","before":"abc","after":"def","compare":"https://github.com","commits":[{{{TestFixtures.CommitJson()}}}],"repository":{{{TestFixtures.RepoJson()}}},"sender":{{{TestFixtures.UserJson()}}},"pusher":{"name":"octocat","email":"octocat@example.com"}}""",
            typeof(PushAction)
        ];
        yield return
        [
            "star",
            $$$"""{"action":"created","repository":{{{TestFixtures.RepoJson()}}},"sender":{{{TestFixtures.UserJson()}}}}""",
            typeof(StarAction)
        ];
    }

    [Theory]
    [MemberData(nameof(KnownEventData))]
    public void GetAction_KnownEvent_ReturnsCorrectType(string eventName, string json, Type expectedType)
    {
        var factory = new ActionFactory(BuildServiceProvider());
        var action = factory.GetAction(eventName, json, _webhookUri);
        Assert.IsType(expectedType, action);
    }
}
