using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// 起動時にアクションレジストリの全エントリーをドライラン検証する <see cref="IHostedService"/>。
/// </summary>
public sealed class ActionRegistryValidator(ActionFactory factory, IServiceProvider sp) : IHostedService
{
    /// <summary>全登録アクションをドライラン検証する。テストから直接呼び出し可能。</summary>
    internal void ValidateAll()
    {
        var dummyUri = new Uri("https://example.invalid");
        const string dummyEventName = "__startup_validate__";

        foreach (var (eventName, (actionType, payloadType)) in factory.Registry)
        {
            // `{}` で初期化できない型（required メンバーを持つ Octokit 型）に備え、
            // デシリアライズ失敗を捕捉してスキップする（ペイロード生成の失敗はアクション実装の問題ではない）。
            object dummy;
            try
            {
                dummy = JsonSerializer.Deserialize("""{}""", payloadType, OctokitJsonOptions.Value)
                        ?? Activator.CreateInstance(payloadType)
                        ?? throw new InvalidOperationException($"Cannot create dummy for {payloadType.Name}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"ActionRegistryValidator: cannot create dummy payload for '{eventName}' " +
                    $"({payloadType.Name}). Octokit type may have required members that prevent " +
                    $"deserialization from '{{}}'. Check if the type has non-nullable required properties. " +
                    $"Inner: {ex.Message}", ex);
            }

            try
            {
                ActivatorUtilities.CreateInstance(sp, actionType, dummyUri, dummyEventName, dummy);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"ActionRegistryValidator: failed to instantiate '{actionType.Name}' for event '{eventName}'. " +
                    $"Ensure all DI dependencies are registered in Program.cs. " +
                    $"Inner: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
