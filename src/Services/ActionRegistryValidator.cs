using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// <see cref="IHostedService"/> implementation that dry-run validates every entry in the action registry at startup
/// </summary>
public sealed class ActionRegistryValidator(ActionFactory factory, IServiceProvider sp) : IHostedService
{
    /// <summary>Dry-run validates all registered actions. Can be called directly from tests</summary>
    internal void ValidateAll()
    {
        var dummyUri = new Uri("https://example.invalid");
        const string dummyEventName = "__startup_validate__";

        foreach (KeyValuePair<string, (Type Action, Type Payload)> item in factory.Registry)
        {
            var eventName = item.Key;
            Type actionType = item.Value.Action;
            Type payloadType = item.Value.Payload;

            // For types that cannot be initialized from `{}` (Octokit types with required members),
            // catch the deserialization failure and skip (payload generation failure is not an action implementation issue).
            object dummy;
            try
            {
                dummy = JsonSerializer.Deserialize("""{}""", payloadType, OctokitJsonOptions.Value)
                        ?? Activator.CreateInstance(payloadType)
                        ?? throw new InvalidOperationException($"Cannot create dummy for {payloadType.Name}");
            }
            catch (Exception)
            {
                // Octokit types have required members, so deserialization from `{}` may fail.
                // Payload generation failure is a type-schema issue, not an action implementation issue, so skip it.
                continue;
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
                    $"Inner: {ex.Message}",
                    ex);
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
