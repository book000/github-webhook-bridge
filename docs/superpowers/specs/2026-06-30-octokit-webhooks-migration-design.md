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
<!-- added -->
<PackageReference Include="Octokit.Webhooks" Version="*" />
```

---

## Component Design

### `[GitHubEvent]` Attribute

Maps an action class to its GitHub event name. No string literal is used at the call site — the event name is derived from the Octokit.Webhooks model type's own attribute (Level 2). If Octokit does not expose that attribute, fall back to `WebhookEventType` constants (Level 1).

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute(string eventName) : Attribute
{
    public string EventName { get; } = eventName;
}
```

**Level 2 usage (preferred):**

```csharp
// No string literal — event name derived from PullRequestWebhookEvent's Octokit attribute
[GitHubEvent]
public sealed class PullRequestAction : BaseAction<PullRequestWebhookEvent> { }
```

**Level 1 fallback (if Octokit does not expose attribute):**

```csharp
[GitHubEvent(WebhookEventType.PullRequest)]   // compile-time constant, not a raw string
public sealed class PullRequestAction : BaseAction<PullRequestWebhookEvent> { }
```

The chosen level is decided at implementation time by inspecting the Octokit.Webhooks API surface.

---

### `ActionFactory` — Reflection Registry

Built once at startup as a `FrozenDictionary`. No switch expression.

```csharp
public class ActionFactory(IServiceProvider sp) : IActionFactory
{
    private readonly FrozenDictionary<string, (Type Action, Type Payload)> _registry =
        BuildRegistry();

    private static FrozenDictionary<string, (Type, Type)> BuildRegistry()
    {
        var entries = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(t =>
            {
                var attr   = t.GetCustomAttribute<GitHubEventAttribute>()!;
                var key    = ResolveEventName(attr, t);   // Level 2 or Level 1
                var payload = GetPayloadType(t);
                return (key, (t, payload));
            });

        return entries.ToFrozenDictionary(e => e.key, e => e.Item2);
    }

    public IAction GetAction(string eventName, string rawJson, Uri webhookUrl)
    {
        if (!_registry.TryGetValue(eventName, out var entry))
            return ActivatorUtilities.CreateInstance<UnhandledAction>(sp, webhookUrl, eventName);

        var payload = JsonSerializer.Deserialize(rawJson, entry.Payload, OctokitJsonOptions.Value)
                      ?? throw new InvalidOperationException($"Deserialization failed: {entry.Payload.Name}");

        return (IAction)ActivatorUtilities.CreateInstance(sp, entry.Action, webhookUrl, payload);
    }

    // Walk base types to extract T from BaseAction<T>
    private static Type GetPayloadType(Type t)
    {
        var b = t.BaseType;
        while (b is { IsGenericType: false }) b = b.BaseType;
        return b?.GetGenericArguments()[0] ?? typeof(JsonElement);
    }
}
```

**Startup validation**: at `Program.cs` startup, `ActionFactory` is instantiated eagerly and all registry entries are validated. If Level 2 attribute resolution fails for any registered type, the application fails fast with a descriptive error rather than breaking at the first request.

---

### `JsonSerializerOptions` Strategy

Octokit.Webhooks may provide its own `JsonSerializerOptions` with custom converters (enum string policies, etc.). The factory uses Octokit's options as the base and layers on leniency settings:

```csharp
internal static class OctokitJsonOptions
{
    // Determined at implementation time — use Octokit's options if exposed,
    // otherwise build from scratch with matching settings.
    public static readonly JsonSerializerOptions Value = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        // Start from Octokit's options (if accessible) or new JsonSerializerOptions()
        var opts = /* OctokitWebhooksDefaults.SerializerOptions ?? */ new JsonSerializerOptions();
        opts.PropertyNameCaseInsensitive   = true;
        opts.AllowTrailingCommas           = true;
        opts.ReadCommentHandling           = JsonCommentHandling.Skip;
        opts.UnmappedMemberHandling        = JsonUnmappedMemberHandling.Skip;
        return opts;
    }
}
```

---

### `IActionFactory` — Updated Signature

```csharp
public interface IActionFactory
{
    // Raw JSON string instead of JsonElement — deserialization happens inside the factory
    IAction GetAction(string eventName, string rawJson, Uri webhookUrl);
}
```

`WebhookFunction.cs` passes the raw request body string (already read for signature verification) directly to the factory. No intermediate `JsonElement` parsing.

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

Single class, no `[GitHubEvent]` attribute — never entered into the registry.

```csharp
public sealed class UnhandledAction(Uri webhookUrl, string eventName, ...) : IAction
{
    public Task RunAsync() =>
        throw new NotImplementedException($"Event '{eventName}' is not implemented.");
    // WebhookFunction catches NotImplementedException → 406 (existing behavior unchanged)
}
```

---

### Adding a New Action (post-redesign workflow)

1. Create a file in `Actions/Impl/`.
2. Attach `[GitHubEvent]` (with event name constant or auto-derived).
3. Extend `BaseAction<OctokitEventModel>` and implement `RunAsync()`.
4. Done — ActionFactory, StubActions, and IActionFactory require no changes.

---

## Testing

### `OctokitWebhooksCompatibilityTests` — Top-Level Event Coverage

Detects new or removed top-level event types after a Renovate update.

```csharp
// KnownEventTypes is the single source of truth — update it when the test fails
private static readonly IReadOnlySet<string> KnownEventTypes = new HashSet<string> { ... };

[Fact]
public void AllOctokitEventTypesMustBeInKnownList()
{
    var actual = typeof(WebhookEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
        .Select(t => t.GetCustomAttribute<WebhookEventAttribute>()!.EventName)
        .ToHashSet();

    Assert.Empty(actual.Except(KnownEventTypes));   // Octokit added new events
    Assert.Empty(KnownEventTypes.Except(actual));   // Octokit removed events
}
```

### `OctokitPayloadSchemaSnapshotTests` — Implemented Model Drift

Detects property additions, removals, type changes, `[JsonPropertyName]` changes, and enum value changes within the payload types used by **implemented** actions. Snapshot is committed to the repository.

```csharp
[Fact]
public void ImplementedPayloadTypes_MustMatchSnapshot()
{
    var payloadTypes = GetImplementedPayloadTypes();   // from [GitHubEvent] classes
    var actual = Serialize(BuildSchema(payloadTypes)); // recursive property + enum walk

    if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
    {
        File.WriteAllText(SnapshotPath, actual);
        return;
    }

    Assert.Equal(File.ReadAllText(SnapshotPath), actual);
}

// BuildSchema recurses into nested types (cycle-safe via visited set)
// and includes:
//   - Property name, CLR type, JsonPropertyName
//   - Enum members (name + underlying value) for any enum-typed property
```

**Snapshot update workflow** (when Renovate updates Octokit.Webhooks and the test fails):

```bash
UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests
# Review the diff, then commit the updated snapshot file
```

### Existing Tests

- `ActionFactoryTests.cs` — rewritten for registry behavior (attribute lookup, `UnhandledAction` fallback, startup validation)
- `*ActionTests.cs` — payload types change from hand-written models to Octokit types; test data construction changes accordingly
- `MonkeyTests.cs` — updated to use Octokit payload types

---

## Risk Register

| Risk | Mitigation |
|---|---|
| Octokit.Webhooks does not expose `[WebhookEvent]` attribute on model classes | Fall back to Level 1 (`WebhookEventType` constants); decide at implementation time |
| Octokit.Webhooks custom JSON converters conflict with our `JsonSerializerOptions` | Adopt Octokit's options as the base; add only additive leniency settings |
| `ActivatorUtilities.CreateInstance` parameter resolution fails at runtime | Validate all registry entries at startup; fail fast with descriptive error |
| Snapshot test file conflicts on concurrent PR branches | Treat snapshot conflict as a merge conflict requiring manual resolution |

---

## Out of Scope

- `SignatureValidator` — no changes
- `MuteManager`, `GitHubUserMapManager`, `BaseManager` — no changes
- `DiscordClient`, `MessageCacheService` — no changes
- `host.json`, `local.settings.json`, CI/CD workflows — no changes
