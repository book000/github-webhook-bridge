using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>ActionFactory のリフレクションレジストリ動作テスト。</summary>
public class ActionFactoryTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    internal static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
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
    public void ActionRegistryValidator_ValidateAll_DoesNotThrowForEmptyRegistry()
    {
        var sp = BuildServiceProvider();
        var factory = new ActionFactory(sp);
        var validator = new ActionRegistryValidator(factory, sp);

        // Task 8 前はレジストリが空なので例外なし
        var ex = Record.Exception(() => validator.ValidateAll());
        Assert.Null(ex);
    }

    // ── Task 8 完了後に Registry.Count == 12 を検証するテストをここに追加する ──
    // Task 8 Step 7c 参照。
}
