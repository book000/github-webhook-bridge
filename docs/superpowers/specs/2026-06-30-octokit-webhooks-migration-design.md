# Design: Octokit.Webhooks Model Adoption + Reflection-Based ActionFactory

**Date**: 2026-06-30  
**Branch**: feat/azure-functions-migration  
**Scope**: PR #2622 — complete redesign of the action routing and model layers, assuming no backward compatibility constraints.

---

## Problem Statement

The current implementation has three structural pain points:

| Problem | Location | Scale |
|---|---|---|
| 58-branch switch expression, all hand-written | `ActionFactory.cs` | ~80 lines of boilerplate |
| 46 one-liner stub classes, all identical | `StubActions.cs` | 251 lines |
| Hand-written models maintained in parallel with auto-generated ones | `Models/GitHubWebhooks/` | Double maintenance cost |

---

## Goals

1. Eliminate the ActionFactory switch expression — new events register themselves.
2. Eliminate StubActions.cs — reduce to a single `UnhandledAction`.
3. Replace hand-written webhook models with `Octokit.Webhooks` NuGet types (compile-time schema guarantee).
4. Keep runtime deserialization lenient — unknown/extra JSON fields are silently ignored.
5. Detect Octokit.Webhooks model drift in CI so Renovate updates don't silently break behavior.

---

## Architecture

### Request Flow (changed parts only)

```
POST /
  WebhookFunction
    SignatureValidator         — unchanged (HMAC-SHA256 on raw body bytes)
    body read as raw string    — JsonElement parse removed; raw string passed to factory
    ActionFactory.GetAction()  — switch removed; reflection registry instead
      [GitHubEvent] attribute lookup
        hit  → instantiate matching Action via ActivatorUtilities
        miss → UnhandledAction → throws NotImplementedException → 406
    action.RunAsync()          — cache / mute / Discord logic unchanged
```

### What Is Deleted

| File / directory | Reason |
|---|---|
| `src/Actions/Stubs/StubActions.cs` | Replaced by single `UnhandledAction` |
| Switch expression in `ActionFactory.cs` | Replaced by reflection registry |
| `src/Models/GitHubWebhooks/*.cs` (hand-written) | Replaced by Octokit.Webhooks types |
| `src/Models/GitHubWebhooks/Generated/*.g.cs` | No longer needed |

### NuGet Changes

```xml
<!-- added — pin to a specific version at implementation time; managed by Renovate thereafter -->
<PackageReference Include="Octokit.Webhooks" Version="X.Y.Z" />
```

`Version="*"` must not be used: the project enables `RestorePackagesWithLockFile`, and a floating reference defeats both the lock file and Renovate's version tracking.

---

## Component Design

### `[GitHubEvent]` Attribute

Maps an action class to its GitHub event name.

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute : Attribute
{
    // Level 1: explicit constant (always available)
    public GitHubEventAttribute(string eventName) => EventName = eventName;

    // Level 2: no-arg — event name derived from the payload type's Octokit attribute
    public GitHubEventAttribute() => EventName = null;

    public string? EventName { get; }
}
```

**Level 2 usage (preferred — verify at implementation time):**

```csharp
// Event name is derived from PullRequestWebhookEvent's own [WebhookEventType] attribute
[GitHubEvent]
public sealed class PullRequestAction : BaseAction<PullRequestWebhookEvent> { }
```

**Level 1 fallback (if Octokit.Webhooks does not expose a resolvable attribute on model types):**

```csharp
[GitHubEvent(WebhookEventType.PullRequest)]  // compile-time constant, not a raw string
public sealed class PullRequestAction : BaseAction<PullRequestWebhookEvent> { }
```

`ResolveEventName()` in `ActionFactory` tries Level 2 first; if the Octokit attribute is absent it uses Level 1's explicit string. If neither is available the registry throws at startup.

**Event name casing**: GitHub always sends lowercase snake_case event names (e.g. `pull_request`). `[GitHubEvent]` values must be lowercase; `ActionFactory` stores and looks up keys as-is without case folding. A wrong-case key in the attribute silently misses every request — the startup validator (see below) exists to catch this.

---

### `ActionFactory` — Reflection Registry

Built once at startup as a `FrozenDictionary`. No switch expression.  
Uses `typeof(ActionFactory).Assembly` instead of `Assembly.GetExecutingAssembly()` to be safe in the Azure Functions Isolated Worker process model.

```csharp
public class ActionFactory : IActionFactory
{
    private readonly IServiceProvider _sp;
    private readonly FrozenDictionary<string, (Type Action, Type Payload)> _registry;

    public ActionFactory(IServiceProvider sp)
    {
        _sp = sp;
        _registry = BuildRegistry();
    }

    private static FrozenDictionary<string, (Type, Type)> BuildRegistry()
    {
        var entries = typeof(ActionFactory).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(t =>
            {
                var payload = GetPayloadType(t);
                var key     = ResolveEventName(t, payload);
                return (key, (t, payload));
            });

        return entries.ToFrozenDictionary(e => e.key, e => e.Item2);
    }

    public IAction GetAction(string eventName, string rawJson, Uri webhookUrl)
    {
        if (!_registry.TryGetValue(eventName, out var entry))
            return ActivatorUtilities.CreateInstance<UnhandledAction>(_sp, webhookUrl, eventName);

        var payload = JsonSerializer.Deserialize(rawJson, entry.Payload, OctokitJsonOptions.Value)
                      ?? throw new InvalidOperationException($"Deserialization failed: {entry.Payload.Name}");

        // Each Action constructor signature: (Uri webhookUrl, TPayload payload, <DI services...>)
        // ActivatorUtilities resolves DI services from _sp and fills runtime args from the params array.
        // Uri and the specific TPayload type are unique in the constructor → no ambiguity.
        return (IAction)ActivatorUtilities.CreateInstance(_sp, entry.Action, webhookUrl, payload);
    }

    // Walk BaseAction<T> specifically to extract T; throw if not found.
    private static Type GetPayloadType(Type t)
    {
        var b = t.BaseType;
        while (b != null && !(b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BaseAction<>)))
            b = b.BaseType;
        if (b == null)
            throw new InvalidOperationException(
                $"{t.Name} must inherit from BaseAction<T> to be registered via [GitHubEvent].");
        return b.GetGenericArguments()[0];
    }

    // Level 2: derive from Octokit attribute on the payload type.
    // Level 1: use the explicit string on [GitHubEvent].
    // If neither is available, fail fast at startup.
    private static string ResolveEventName(Type actionType, Type payloadType)
    {
        // Level 2 — inspect at implementation time for the exact attribute name Octokit uses
        var octokitAttr = payloadType.GetCustomAttribute<WebhookEventAttribute>();
        if (octokitAttr != null)
            return octokitAttr.EventName;

        // Level 1
        var explicitName = actionType.GetCustomAttribute<GitHubEventAttribute>()!.EventName;
        if (explicitName != null)
            return explicitName;

        throw new InvalidOperationException(
            $"Cannot resolve event name for {actionType.Name}: " +
            $"payload type {payloadType.Name} has no Octokit [WebhookEventAttribute] " +
            $"and [GitHubEvent] was declared without an explicit name.");
    }
}
```

---

### Startup Validation

`ActionFactory.BuildRegistry()` runs in the constructor, so a misconfigured action (bad inheritance, unresolvable event name) throws at startup rather than at the first request.

For validating that `ActivatorUtilities.CreateInstance` can actually resolve every action type, a dedicated `IHostedService` is added:

```csharp
internal sealed class ActionRegistryValidator(IActionFactory factory) : IHostedService
{
    public Task StartAsync(CancellationToken _)
    {
        // Cast to the concrete type to access the registry without exposing it on the interface
        ((ActionFactory)factory).ValidateAll();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken _) => Task.CompletedTask;
}

// In ActionFactory:
internal void ValidateAll()
{
    foreach (var (key, (actionType, payloadType)) in _registry)
    {
        // Attempt dry-run instantiation with a dummy payload to verify DI resolution.
        // Uses a sentinel Uri and a default-constructed payload (or JsonSerializer default).
        var dummy = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(new { }), payloadType, OctokitJsonOptions.Value)!;
        ActivatorUtilities.CreateInstance(_sp, actionType, new Uri("https://example.com"), dummy);
    }
}
```

Registered in `Program.cs`:

```csharp
builder.Services.AddHostedService<ActionRegistryValidator>();
```

---

### `WebhookFunction` — Body Reading Changes

**Before**: body read as bytes → raw string for HMAC → parsed to `JsonElement` → passed to factory.  
**After**: body read as bytes → raw string for HMAC → raw string passed to factory directly.

The mute check (step 8 in the current function) reads `sender.id` from `JsonElement`. With `JsonElement` removed, this is replaced by a lightweight `Utf8JsonReader` or a minimal `JsonSerializer.Deserialize` into an anonymous/shared record that captures only `sender`:

```csharp
// Minimal shared record used only for mute pre-check in WebhookFunction
internal sealed record WebhookSender(long Id);
internal sealed record WebhookEnvelope(WebhookSender? Sender);

// In WebhookFunction:
var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawBody, OctokitJsonOptions.Value);
var senderId = envelope?.Sender?.Id;
```

This keeps `WebhookFunction` independent of the full Octokit model hierarchy for the mute check.

---

### `JsonSerializerOptions` Strategy

Octokit.Webhooks may provide its own `JsonSerializerOptions` with custom converters (enum string policies, etc.). The factory uses Octokit's options as the base and layers on leniency settings. The instance is made read-only after construction to prevent accidental mutation across tests.

```csharp
internal static class OctokitJsonOptions
{
    public static readonly JsonSerializerOptions Value = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        // Start from Octokit's options if exposed; otherwise start from scratch.
        // Determine the correct base at implementation time.
        var opts = /* OctokitWebhooksDefaults.SerializerOptions?.Clone() ?? */ new JsonSerializerOptions();
        opts.PropertyNameCaseInsensitive   = true;
        opts.AllowTrailingCommas           = true;
        opts.ReadCommentHandling           = JsonCommentHandling.Skip;
        opts.UnmappedMemberHandling        = JsonUnmappedMemberHandling.Skip;
        opts.MakeReadOnly();
        return opts;
    }
}
```

---

### `IActionFactory` — Updated Signature

```csharp
public interface IActionFactory
{
    // Raw JSON string instead of JsonElement — deserialization happens inside the factory.
    IAction GetAction(string eventName, string rawJson, Uri webhookUrl);
}
```

---

### `BaseAction<T>` — Type Constraint Only Change

```csharp
// Before
public abstract class BaseAction<TEvent>( ... )

// After
public abstract class BaseAction<TEvent>( ... )
    where TEvent : WebhookEvent   // Octokit.Webhooks base type
```

All internal logic (5-minute cache edit, mute check, Discord send) is unchanged.

---

### `UnhandledAction`

Does not extend `BaseAction<T>` — it has no payload to process and requires no Octokit type. Only the minimal DI dependencies needed to return a 406 are injected.

```csharp
// No [GitHubEvent] attribute — never entered into the registry.
public sealed class UnhandledAction(ILogger<UnhandledAction> logger) : IAction
{
    // webhookUrl and eventName are passed as runtime args by ActionFactory
    private string _eventName = string.Empty;

    internal void SetContext(string eventName) => _eventName = eventName;

    public Task RunAsync() =>
        throw new NotImplementedException($"Event '{_eventName}' is not implemented.");
    // WebhookFunction catches NotImplementedException → 406 (existing behavior unchanged)
}
```

> **Note**: The exact constructor shape for `UnhandledAction` (how `eventName` is threaded in) may be adjusted at implementation time depending on how `ActivatorUtilities` resolves parameters. A simple alternative is a factory method rather than `ActivatorUtilities` for the unhandled case.

---

### Adding a New Action (post-redesign workflow)

1. Create a file in `Actions/Impl/`.
2. Attach `[GitHubEvent]` (Level 2 preferred, Level 1 as fallback).
3. Extend `BaseAction<OctokitEventModel>` and implement `RunAsync()`.
4. Done — `ActionFactory`, `StubActions`, and `IActionFactory` require no changes.

---

## Testing

### `OctokitWebhooksCompatibilityTests` — Top-Level Event Coverage

Detects new or removed top-level event types after a Renovate update.

```csharp
// KnownEventTypes is the single source of truth — update it when the test fails.
private static readonly IReadOnlySet<string> KnownEventTypes = new HashSet<string> { ... };

[Fact]
public void AllOctokitEventTypesMustBeInKnownList()
{
    var actual = typeof(WebhookEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
        .Select(t => t.GetCustomAttribute<WebhookEventAttribute>()!.EventName)
        .ToHashSet();

    var added   = actual.Except(KnownEventTypes).ToHashSet();
    var removed = KnownEventTypes.Except(actual).ToHashSet();

    Assert.True(added.Count   == 0, $"Octokit.Webhooks added new events: {Join(added)}. Implement or add to KnownEventTypes.");
    Assert.True(removed.Count == 0, $"Octokit.Webhooks removed events: {Join(removed)}. Remove from KnownEventTypes.");

    static string Join(IEnumerable<string> s) => string.Join(", ", s.OrderBy(x => x));
}
```

### `OctokitPayloadSchemaSnapshotTests` — Implemented Model Drift

Detects property additions, removals, type changes, `[JsonPropertyName]` changes, and enum value changes within the payload types used by **implemented** actions. The snapshot file is committed to the repository.

**Snapshot path**: `tests/GitHubWebhookBridge.Tests/Snapshots/octokit-payload-schema.json`.  
Resolved at runtime via `Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Snapshots", "octokit-payload-schema.json")` so it is stable across `dotnet test` invocations regardless of working directory.

```csharp
[Fact]
public void ImplementedPayloadTypes_MustMatchSnapshot()
{
    var payloadTypes = GetImplementedPayloadTypes();   // from [GitHubEvent] classes
    var actual       = JsonSerializer.Serialize(BuildSchema(payloadTypes), PrettyOptions);

    if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
    {
        File.WriteAllText(SnapshotPath, actual);
        return;
    }

    var expected = File.ReadAllText(SnapshotPath);
    Assert.True(expected == actual,
        $"Octokit.Webhooks model schema has changed. Run UPDATE_SNAPSHOTS=1 dotnet test, " +
        $"review the diff in {SnapshotPath}, then commit.");
}

// BuildSchema:
//   - Recurses into nested types (cycle-safe via visited HashSet<Type>)
//   - Captures: property CLR name, JsonPropertyName, CLR type name
//   - For enum-typed properties: captures all member names and underlying int values
private static Dictionary<string, object> BuildSchema(IEnumerable<Type> types) { ... }
```

**Snapshot update workflow** (when Renovate updates Octokit.Webhooks and the test fails):

```bash
UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests
# Review the diff in tests/.../Snapshots/octokit-payload-schema.json
# Commit the updated snapshot
```

### Existing Tests

- `ActionFactoryTests.cs` — rewritten for registry behavior (attribute lookup, `UnhandledAction` fallback, startup validation)
- `*ActionTests.cs` — payload types change from hand-written models to Octokit types; test data construction changes accordingly
- `MonkeyTests.cs` — updated to use Octokit payload types

---

## Risk Register

| Risk | Mitigation |
|---|---|
| Octokit.Webhooks does not expose a `[WebhookEvent]`-style attribute on model classes | Fall back to Level 1 (`WebhookEventType` constants); `ResolveEventName()` handles both paths |
| Octokit.Webhooks custom JSON converters conflict with our `JsonSerializerOptions` | Adopt Octokit's options as the base (clone if API allows); add only additive leniency settings on top |
| `ActivatorUtilities.CreateInstance` parameter resolution fails at runtime | `Uri` and the specific `TPayload` type are unique in each constructor — no ambiguity. `ActionRegistryValidator` performs a dry-run of every entry at startup |
| `ActivatorUtilities` `string`-typed parameters collide | `UnhandledAction` is not instantiated via `ActivatorUtilities` if a simpler factory method is used; for normal actions `string` params are avoided in constructors (use typed Octokit models instead) |
| Snapshot file conflicts on concurrent PR branches | Treat as a merge conflict requiring manual resolution; prefer rebasing over merging |
| `[GitHubEvent]` declared with wrong-case event name | `ActionRegistryValidator` catches missing keys at startup; case-sensitive `FrozenDictionary` is intentional — all GitHub event names are lowercase |
| Renovate updates Octokit.Webhooks and new events are silently ignored | `OctokitWebhooksCompatibilityTests` fails CI with the list of new event names |
| Renovate updates Octokit.Webhooks and implemented model properties change | `OctokitPayloadSchemaSnapshotTests` fails CI with a path to the diff |

---

## Out of Scope

- `SignatureValidator` — no changes
- `MuteManager`, `GitHubUserMapManager`, `BaseManager` — no changes
- `DiscordClient`, `MessageCacheService` — no changes
- `host.json`, `local.settings.json`, CI/CD workflows — no changes
