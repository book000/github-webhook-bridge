using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// リフレクションによりアセンブリをスキャンし、
/// <see cref="GitHubEventAttribute"/> 付きクラスを自動登録するアクションファクトリ。
/// </summary>
public class ActionFactory(IServiceProvider sp) : IActionFactory
{
    private readonly IServiceProvider _sp = sp;

    private readonly FrozenDictionary<string, (Type Action, Type Payload)> _registry =
        BuildRegistry();

    private static FrozenDictionary<string, (Type, Type)> BuildRegistry()
    {
        var entries = typeof(ActionFactory).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(t =>
            {
                var payloadType = GetPayloadType(t);
                var eventName   = ResolveEventName(t, payloadType);
                return KeyValuePair.Create(eventName, (t, payloadType));
            });

        // キーはすべて小文字スネークケース（GitHub のイベント名仕様）。大文字小文字を混在させないこと。
        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Type GetPayloadType(Type actionType)
    {
        var b = actionType.BaseType;
        while (b != null && !(b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BaseAction<>)))
            b = b.BaseType;
        if (b == null)
            throw new InvalidOperationException(
                $"{actionType.Name} must inherit from BaseAction<T> to be registered via [GitHubEvent].");
        return b.GetGenericArguments()[0];
    }

    private static string ResolveEventName(Type actionType, Type payloadType)
    {
        // --- Level 2 (scratch/octokit-api-surface.md の Level2Available が true の場合) ---
        // var octokitAttr = payloadType.GetCustomAttribute<LEVEL2_ATTRIBUTE_TYPE>();
        // if (octokitAttr != null) return octokitAttr.LEVEL2_PROPERTY_NAME;

        // --- Level 1 ---
        var attr = actionType.GetCustomAttribute<GitHubEventAttribute>()!;
        if (attr.EventName != null)
            return attr.EventName;

        throw new InvalidOperationException(
            $"Cannot resolve event name for {actionType.Name}: " +
            $"payload type {payloadType.Name} has no Level 2 Octokit attribute and " +
            $"[GitHubEvent] was declared without an explicit name.");
    }

    /// <inheritdoc/>
    public IAction GetAction(string eventName, string rawJson, Uri webhookUrl)
    {
        if (!_registry.TryGetValue(eventName, out var entry))
            return new UnhandledAction(eventName);

        var payload = JsonSerializer.Deserialize(rawJson, entry.Payload, OctokitJsonOptions.Value)
                      ?? throw new InvalidOperationException(
                          $"Deserialization returned null for event '{eventName}' ({entry.Payload.Name}).");

        return (IAction)ActivatorUtilities.CreateInstance(_sp, entry.Action, webhookUrl, eventName, payload);
    }

    /// <summary>テスト・ActionRegistryValidator 用: レジストリを内部から参照できるようにする。</summary>
    internal IReadOnlyDictionary<string, (Type Action, Type Payload)> Registry => _registry;
}
