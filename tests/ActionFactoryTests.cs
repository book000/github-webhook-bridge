using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.DependencyInjection;
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
}
