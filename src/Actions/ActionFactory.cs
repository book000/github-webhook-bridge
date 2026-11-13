using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using PullRequestEditedEvent = Octokit.Webhooks.Events.PullRequest.PullRequestEditedEvent;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// Scans the assembly via reflection and automatically registers
/// action handlers annotated with <see cref="GitHubEventAttribute"/>.
/// </summary>
public class ActionFactory(IServiceProvider sp) : IActionFactory
{
    private readonly IServiceProvider _sp = sp;

    private readonly FrozenDictionary<string, (Type Action, Type Payload)> _registry =
        BuildRegistry();

    private static FrozenDictionary<string, (Type Action, Type Payload)> BuildRegistry()
    {
        IEnumerable<KeyValuePair<string, (Type Action, Type Payload)>> entries = typeof(ActionFactory).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(t =>
            {
                Type payloadType = GetPayloadType(t);
                var eventName = ResolveEventName(t, payloadType);
                return KeyValuePair.Create(eventName, (t, payloadType));
            });

        // Keys are all lowercase snake_case (GitHub's event name convention). Never mix casing.
        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Type GetPayloadType(Type actionType)
    {
        Type? b = actionType.BaseType;
        while (b != null && !(b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BaseAction<>)))
            b = b.BaseType;

        if (b == null)
        {
            throw new InvalidOperationException(
                $"{actionType.Name} must inherit from BaseAction<T> to be registered via [GitHubEvent].");
        }

        return b.GetGenericArguments()[0];
    }

    private static string ResolveEventName(Type actionType, Type payloadType)
    {
        GitHubEventAttribute attr = actionType.GetCustomAttribute<GitHubEventAttribute>()!;
        if (attr.EventName != null)
            return attr.EventName;

        throw new InvalidOperationException(
            $"Cannot resolve event name for {actionType.Name} (payload: {payloadType.Name}): " +
            $"[GitHubEvent] requires an explicit event name.");
    }

    /// <inheritdoc/>
    public bool IsRegistered(string eventName) => _registry.ContainsKey(eventName);

    /// <inheritdoc/>
    public IAction GetAction(string eventName, string rawJson, Uri webhookUrl)
    {
        if (!_registry.TryGetValue(eventName, out (Type Action, Type Payload) entry))
            return new UnhandledAction(eventName);

        var payload = JsonSerializer.Deserialize(rawJson, entry.Payload, OctokitJsonOptions.Value)
                      ?? throw new InvalidOperationException(
                          $"Deserialization returned null for event '{eventName}' ({entry.Payload.Name}).");

        // ActivatorUtilities rejects arguments that no constructor consumes, so RawPayload is passed only to actions that declare it.
        object[] args = AcceptsRawPayload(entry.Action)
            ? [webhookUrl, eventName, payload, new RawPayload(rawJson)]
            : [webhookUrl, eventName, payload];

        var action = (IAction)ActivatorUtilities.CreateInstance(_sp, entry.Action, args);

        if (action is PullRequestAction pullRequestAction && payload is PullRequestEditedEvent)
            pullRequestAction.PreviousBaseRef = ReadPreviousBaseRef(rawJson);

        return action;
    }

    private static bool AcceptsRawPayload(Type actionType) =>
        actionType.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(RawPayload)));

    /// <summary>
    /// Reads <c>changes.base.ref.from</c> from a pull_request edited payload.
    /// Returns null when any level is missing or has an unexpected type.
    /// </summary>
    private static string? ReadPreviousBaseRef(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        JsonElement node = doc.RootElement;
        foreach (var name in new[] { "changes", "base", "ref" })
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out node))
                return null;
        }

        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("from", out JsonElement from))
            return null;

        return from.ValueKind == JsonValueKind.String && from.GetString() is { Length: > 0 } value ? value : null;
    }

    /// <summary>Gets the registry (for tests and ActionRegistryValidator).</summary>
    internal IReadOnlyDictionary<string, (Type Action, Type Payload)> Registry => _registry;
}
