# Octokit.Webhooks Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hand-written webhook models and the 58-branch ActionFactory switch with `Octokit.Webhooks` NuGet types and a reflection-based registry that auto-discovers actions via `[GitHubEvent]` attribute.

**Architecture:** `ActionFactory` builds a `FrozenDictionary<string, (Type, Type)>` at startup by scanning `[GitHubEvent]`-annotated classes. Event names are resolved from `WebhookEventType` constants (Level 1) or derived from the Octokit model's own attribute (Level 2 — verified in Task 1). `ActivatorUtilities.CreateInstance` instantiates each action with a mix of DI services and runtime arguments (webhookUrl, eventName, payload). `StubActions.cs` and hand-written models are deleted entirely.

**Tech Stack:** C# / .NET 10, Azure Functions v4 Isolated, xUnit, Moq, `Octokit.Webhooks` NuGet, `System.Collections.Frozen`

## Global Constraints

- `RestorePackagesWithLockFile` is enabled — pin `Octokit.Webhooks` to a specific version, never `*`
- All code comments and XML doc: Japanese. Error / log messages: English.
- Never use `#pragma warning disable` — fix the root cause instead.
- Never instantiate `HttpClient` directly — use `IHttpClientFactory`.
- `dotnet build -c Release` must produce zero warnings.
- `dotnet test -c Release` must stay green.
- Line coverage ≥ 80%, mutation score ≥ 60%.
- Test files live flat in `tests/` (e.g., `tests/PullRequestActionTests.cs`).
- Source files live in `src/` with namespace `GitHubWebhookBridge`.
- **Action constructors must not have side effects** (no HTTP calls, no external resource access). This is required for `ActionRegistryValidator` dry-run to be safe.

---

## File Map

| Status | Path | Responsibility |
|---|---|---|
| **Create** | `scratch/octokit-api-surface.md` | Task 1 investigation results — consumed by Tasks 5, 8, 10, 11 |
| **Create** | `src/Actions/GitHubEventAttribute.cs` | Maps action class to event name |
| **Create** | `src/Actions/UnhandledAction.cs` | Returns 406 for all unregistered events |
| **Create** | `src/Utils/OctokitJsonOptions.cs` | Shared lenient `JsonSerializerOptions` |
| **Create** | `src/Services/ActionRegistryValidator.cs` | `IHostedService` — validates registry at startup |
| **Create** | `src/Functions/WebhookEnvelope.cs` | Minimal record for mute-check sender extraction |
| **Create** | `tests/OctokitWebhooksCompatibilityTests.cs` | Detects new/removed top-level events after Renovate |
| **Create** | `tests/OctokitPayloadSchemaSnapshotTests.cs` | Detects property/enum drift in implemented models |
| **Create** | `tests/Snapshots/octokit-payload-schema.json` | Generated snapshot — committed to repo |
| **Modify** | `src/GitHubWebhookBridge.csproj` | Add Octokit.Webhooks, remove Generated Compile Remove |
| **Modify** | `src/Actions/IActionFactory.cs` | Change body param `JsonElement` → `string` |
| **Modify** | `src/Actions/ActionFactory.cs` | Replace switch with reflection registry |
| **Modify** | `src/Actions/BaseAction.cs` | Add `where TEvent : WebhookEvent` constraint |
| **Modify** | `src/Actions/Impl/*.cs` (12 files) | Use Octokit types instead of hand-written models |
| **Modify** | `src/Functions/WebhookFunction.cs` | Keep 400 for invalid JSON; use WebhookEnvelope for mute check |
| **Modify** | `src/Program.cs` | Register `ActionRegistryValidator` as `IHostedService` |
| **Modify** | `tests/ActionFactoryTests.cs` | Rewrite for reflection-registry behavior |
| **Modify** | `tests/*ActionTests.cs` (12 files) | Update helpers to build Octokit payload types |
| **Modify** | `tests/MonkeyTests.cs` | Remove Stubs import; update action constructors |
| **Modify** | `tests/WebhookFunctionTests.cs` | Update `IActionFactory` mock signature; add envelope tests |
| **Modify** | `tests/ActionCoverageTests.cs` | Switch to `[GitHubEvent]`-based coverage check |
| **Delete** | `src/Actions/Stubs/StubActions.cs` | Replaced by `UnhandledAction` |
| **Delete** | `src/Models/GitHubWebhooks/Common.cs` + 11 event files | Replaced by Octokit.Webhooks types |
| **Delete** | `src/Models/GitHubWebhooks/Generated/` (entire dir) | No longer needed |

---

## Task 1: Add Octokit.Webhooks NuGet and Document API Surface

**Files:**
- Modify: `src/GitHubWebhookBridge.csproj`
- Create: `scratch/octokit-api-surface.md`

**Interfaces:**
- Produces: `scratch/octokit-api-surface.md` — **consumed verbatim by Tasks 5, 8, 10, 11**. Do NOT proceed past Task 1 without completing this file.

- [ ] **Step 1: Add the package**

```bash
dotnet add src/GitHubWebhookBridge.csproj package Octokit.Webhooks
```

Confirm a concrete version was pinned in `src/GitHubWebhookBridge.csproj` (not `*`).

- [ ] **Step 2: Write a discovery xUnit test**

Create `tests/OctokitApiSurfaceDiscovery.cs` (temporary — deleted in Step 4):

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit.Webhooks;
using Xunit.Abstractions;

namespace GitHubWebhookBridge.Tests;

/// <summary>Octokit.Webhooks API surface 調査用一時テスト。Task 1 完了後に削除する。</summary>
public class OctokitApiSurfaceDiscovery(ITestOutputHelper output)
{
    [Fact]
    public void DiscoverApiSurface()
    {
        var assembly = typeof(WebhookEvent).Assembly;

        // 1. WebhookEvent サブクラス一覧
        var eventTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();
        output.WriteLine($"=== WebhookEvent subclass count: {eventTypes.Count} ===");
        foreach (var t in eventTypes)
            output.WriteLine($"  {t.Name} ({t.Namespace})");

        // 2. クラスレベル属性の確認（Level 2 判定）
        output.WriteLine("\n=== Class-level attributes on PullRequestEvent ===");
        var prType = eventTypes.FirstOrDefault(t => t.Name.Contains("PullRequest") && !t.Name.Contains("Review") && !t.Name.Contains("Comment") && !t.Name.Contains("Thread"));
        if (prType != null)
        {
            foreach (var a in prType.GetCustomAttributes(inherit: false))
                output.WriteLine($"  {a.GetType().FullName}: {JsonSerializer.Serialize(a, a.GetType())}");
        }

        // 3. WebhookEventType 定数の確認
        var etType = assembly.GetTypes().FirstOrDefault(t => t.Name == "WebhookEventType");
        if (etType != null)
        {
            output.WriteLine("\n=== WebhookEventType constants (sample) ===");
            foreach (var f in etType.GetFields(BindingFlags.Public | BindingFlags.Static).Take(10))
                output.WriteLine($"  {f.Name} = \"{f.GetValue(null)}\"");
        }
        else output.WriteLine("\n=== WebhookEventType not found ===");

        // 4. JsonSerializerOptions の公開確認
        var optionsType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name.Contains("Options") || t.Name.Contains("Defaults") || t.Name.Contains("Serializer"));
        output.WriteLine($"\n=== Potential options type: {optionsType?.FullName ?? "not found"} ===");

        // 5. PullRequestEvent の主要プロパティ型を確認
        if (prType != null)
        {
            output.WriteLine($"\n=== {prType.Name} key properties ===");
            foreach (var p in prType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.Name is "Action" or "PullRequest" or "Repository" or "Sender" or "Number"))
            {
                var jsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name;
                output.WriteLine($"  {p.Name} ({p.PropertyType.Name}) → json:\"{jsonName}\"");
            }
            // PullRequest ネスト型のプロパティも確認
            var prProp = prType.GetProperty("PullRequest");
            if (prProp != null)
            {
                output.WriteLine($"\n=== {prProp.PropertyType.Name} key properties ===");
                foreach (var p in prProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.Name is "HtmlUrl" or "Title" or "Number" or "State" or "Merged" or "Draft" or "User" or "Head" or "Base" or "Body"))
                {
                    var jsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name;
                    output.WriteLine($"  {p.Name} ({p.PropertyType.Name}) → json:\"{jsonName}\"");
                }
            }
        }

        // 6. Action プロパティが enum か string か
        output.WriteLine("\n=== Action property types per event ===");
        foreach (var t in eventTypes.Take(5))
        {
            var actionProp = t.GetProperty("Action");
            if (actionProp != null)
                output.WriteLine($"  {t.Name}.Action → {actionProp.PropertyType.Name} (IsEnum={actionProp.PropertyType.IsEnum})");
        }
    }
}
```

- [ ] **Step 3: Run the discovery test and capture output**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "OctokitApiSurfaceDiscovery" -v n 2>&1 | tee scratch/discovery-raw.txt
```

- [ ] **Step 4: Write `scratch/octokit-api-surface.md` from the output**

Create `scratch/octokit-api-surface.md` and fill in ALL of the following fields based on the test output. This file is the single source of truth for all downstream tasks.

```markdown
# Octokit.Webhooks API Surface (auto-filled from Task 1)

## Package version
<!-- fill in from csproj -->
Octokit.Webhooks X.Y.Z

## Level 2 feasibility
<!-- Does PullRequestEvent have a [WebhookEvent("pull_request")] or similar class-level attribute? -->
Level2Available: true/false
Level2AttributeType: <!-- e.g. "Octokit.Webhooks.WebhookEventTypeAttribute" or "N/A" -->
Level2PropertyName: <!-- e.g. "EventType" or "N/A" -->

## WebhookEventType constants
<!-- Do constants like WebhookEventType.PullRequest = "pull_request" exist? -->
WebhookEventTypeExists: true/false

## JsonSerializerOptions
<!-- Does Octokit expose its own options? -->
OctokitOptionsType: <!-- e.g. "Octokit.Webhooks.WebhookSerializer" or "N/A" -->
OctokitOptionsProperty: <!-- e.g. ".Options" or "N/A" -->

## Action property type
<!-- Is the "action" field on event types a string or enum? -->
ActionType: string/enum
ActionEnumNamespace: <!-- e.g. "Octokit.Webhooks.Events.PullRequest" or "N/A" -->

## HtmlUrl property type
<!-- Is HtmlUrl a System.Uri or string? -->
HtmlUrlType: Uri/string

## Sender.Id type
<!-- Is sender.id long or int? -->
SenderIdType: long/int

## Full event type list (copy all lines from discovery output)
<!-- One per line: TypeName (Namespace) -->
```

- [ ] **Step 5: Delete the temporary discovery test**

```bash
rm tests/OctokitApiSurfaceDiscovery.cs scratch/discovery-raw.txt
```

- [ ] **Step 6: Update packages.lock.json**

```bash
dotnet restore --force-evaluate
```

- [ ] **Step 7: Verify build**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add src/GitHubWebhookBridge.csproj src/packages.lock.json scratch/octokit-api-surface.md
git commit -m "chore: Octokit.Webhooks を追加・API サーフェス調査結果を記録"
```

---

## Task 2: OctokitJsonOptions

**Files:**
- Create: `src/Utils/OctokitJsonOptions.cs`

**Interfaces:**
- Produces: `OctokitJsonOptions.Value` — `JsonSerializerOptions` used by `ActionFactory`, `WebhookFunction`, and `ActionRegistryValidator`.

- [ ] **Step 1: Write the failing test**

Create `tests/OctokitJsonOptionsTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

public class OctokitJsonOptionsTests
{
    [Fact]
    public void Value_PropertyNameCaseInsensitiveIsTrue()
        => Assert.True(OctokitJsonOptions.Value.PropertyNameCaseInsensitive);

    [Fact]
    public void Value_AllowTrailingCommasIsTrue()
        => Assert.True(OctokitJsonOptions.Value.AllowTrailingCommas);

    [Fact]
    public void Value_UnknownFieldsAreIgnored()
    {
        var json = """{"known":"hello","unknown_field":42}""";
        var result = JsonSerializer.Deserialize<KnownOnly>(json, OctokitJsonOptions.Value);
        Assert.Equal("hello", result!.Known);
    }

    [Fact]
    public void Value_IsReadOnly()
        => Assert.Throws<InvalidOperationException>(
            () => OctokitJsonOptions.Value.AllowTrailingCommas = false);

    private sealed record KnownOnly([property: JsonPropertyName("known")] string Known);
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "OctokitJsonOptionsTests" -v n
```

Expected: build error — `OctokitJsonOptions` does not exist yet.

- [ ] **Step 3: Implement**

> **Read `scratch/octokit-api-surface.md` first** — if `OctokitOptionsType` is not "N/A", use that as the base options.

Create `src/Utils/OctokitJsonOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// Octokit.Webhooks モデルのデシリアライズに使用する共有 <see cref="JsonSerializerOptions"/>。
/// </summary>
internal static class OctokitJsonOptions
{
    /// <summary>読み取り専用の共有オプションインスタンス。</summary>
    public static readonly JsonSerializerOptions Value = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        // scratch/octokit-api-surface.md の OctokitOptionsType が "N/A" でない場合:
        //   var opts = <OctokitOptionsType>.<OctokitOptionsProperty>.Clone() など
        // "N/A" の場合は以下のまま。
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive   = true,
            AllowTrailingCommas           = true,
            ReadCommentHandling           = JsonCommentHandling.Skip,
            UnmappedMemberHandling        = JsonUnmappedMemberHandling.Skip,
        };
        opts.MakeReadOnly();
        return opts;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "OctokitJsonOptionsTests" -v n
```

Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Utils/OctokitJsonOptions.cs tests/OctokitJsonOptionsTests.cs
git commit -m "feat: OctokitJsonOptions を追加"
```

---

## Task 3: `[GitHubEvent]` Attribute

**Files:**
- Create: `src/Actions/GitHubEventAttribute.cs`

**Interfaces:**
- Produces: `GitHubEventAttribute` — `[GitHubEvent]` (no-arg, Level 2) and `[GitHubEvent("event_name")]` (Level 1).

- [ ] **Step 1: Write the failing test**

Create `tests/GitHubEventAttributeTests.cs`:

```csharp
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

public class GitHubEventAttributeTests
{
    [Fact]
    public void Level1_StoresEventName()
    {
        var attr = new GitHubEventAttribute("pull_request");
        Assert.Equal("pull_request", attr.EventName);
    }

    [Fact]
    public void Level2_EventNameIsNull()
    {
        var attr = new GitHubEventAttribute();
        Assert.Null(attr.EventName);
    }

    [Fact]
    public void Attribute_TargetsClassOnly()
    {
        var usage = typeof(GitHubEventAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.Inherited);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "GitHubEventAttributeTests" -v n
```

- [ ] **Step 3: Implement**

Create `src/Actions/GitHubEventAttribute.cs`:

```csharp
namespace GitHubWebhookBridge.Actions;

/// <summary>
/// GitHub Webhook イベントハンドラークラスをイベント名に紐付ける属性。
/// <para>
/// Level 1（明示的な定数）: <c>[GitHubEvent(WebhookEventType.PullRequest)]</c><br/>
/// Level 2（自動導出）: <c>[GitHubEvent]</c> — ペイロード型の Octokit 属性からイベント名を導出する。
/// Task 1 の調査で Level 2 が使えない場合は Level 1 のみ使用する。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute : Attribute
{
    /// <summary>イベント名を明示指定する Level 1 コンストラクター。</summary>
    /// <param name="eventName">GitHub Webhook イベント名（小文字スネークケース）。</param>
    public GitHubEventAttribute(string eventName) => EventName = eventName;

    /// <summary>ペイロード型から自動導出する Level 2 コンストラクター。</summary>
    public GitHubEventAttribute() => EventName = null;

    /// <summary>
    /// Level 1 で指定されたイベント名。Level 2 の場合は <see langword="null"/>。
    /// </summary>
    public string? EventName { get; }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "GitHubEventAttributeTests" -v n
```

Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Actions/GitHubEventAttribute.cs tests/GitHubEventAttributeTests.cs
git commit -m "feat: [GitHubEvent] 属性を追加"
```

---

## Task 4: `UnhandledAction`

**Files:**
- Create: `src/Actions/UnhandledAction.cs`

**Interfaces:**
- Produces: `UnhandledAction` — implements `IAction`, no `[GitHubEvent]` attribute, throws `NotImplementedException` (→ 406 in `WebhookFunction`).

- [ ] **Step 1: Write the failing test**

Create `tests/UnhandledActionTests.cs`:

```csharp
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

public class UnhandledActionTests
{
    [Fact]
    public async Task RunAsync_ThrowsNotImplementedException()
    {
        var action = new UnhandledAction("workflow_run");
        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    [Fact]
    public async Task RunAsync_ExceptionMessageContainsEventName()
    {
        var action = new UnhandledAction("some_event");
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
        Assert.Contains("some_event", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnhandledAction_HasNoGitHubEventAttribute()
    {
        var attr = typeof(UnhandledAction)
            .GetCustomAttributes(typeof(GitHubEventAttribute), inherit: false);
        Assert.Empty(attr);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "UnhandledActionTests" -v n
```

- [ ] **Step 3: Implement**

Create `src/Actions/UnhandledAction.cs`:

```csharp
namespace GitHubWebhookBridge.Actions;

/// <summary>
/// 未実装イベントのフォールバックハンドラー。
/// <see cref="GitHubEventAttribute"/> を持たないため <see cref="ActionFactory"/> のレジストリに登録されない。
/// 呼び出されると常に <see cref="NotImplementedException"/> をスローし、
/// <see cref="GitHubWebhookBridge.Functions.WebhookFunction"/> が HTTP 406 に変換する。
/// </summary>
public sealed class UnhandledAction(string eventName) : IAction
{
    /// <inheritdoc/>
    public Task RunAsync()
        => throw new NotImplementedException($"Event '{eventName}' is not implemented.");
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "UnhandledActionTests" -v n
```

Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Actions/UnhandledAction.cs tests/UnhandledActionTests.cs
git commit -m "feat: UnhandledAction を追加（HTTP 406 フォールバック）"
```

---

## Task 5: `IActionFactory` + `ActionFactory` Refactor

**Files:**
- Modify: `src/Actions/IActionFactory.cs`
- Modify: `src/Actions/ActionFactory.cs`
- Modify: `tests/ActionFactoryTests.cs`

**Interfaces:**
- Consumes: `GitHubEventAttribute` (Task 3), `UnhandledAction` (Task 4), `OctokitJsonOptions` (Task 2)
- Produces: `IActionFactory.GetAction(string eventName, string rawJson, Uri webhookUrl)`

> **Before starting: read `scratch/octokit-api-surface.md`** and apply the Level 2 implementation in `ResolveEventName` if `Level2Available: true`.

> **Note on `string eventName` type safety**: `ActivatorUtilities` fills params from the array by type. `string` has no registered DI entry, so `eventName` will always be filled from the params array. This is safe for the current `Program.cs`. Add the following comment to `Program.cs` when registering `ActionFactory`: "// CAPTIVE DEPENDENCY GUARD: ActionFactory が受け取る IServiceProvider は root SP。Action の依存はすべて Singleton であること。Scoped サービスを Action に追加した場合は IServiceScopeFactory を使う設計に変更すること。"

- [ ] **Step 1: Update `IActionFactory` signature**

Edit `src/Actions/IActionFactory.cs`:

```csharp
namespace GitHubWebhookBridge.Actions;

/// <summary>イベント名から IAction を生成するファクトリインターフェース。</summary>
public interface IActionFactory
{
    /// <summary>イベント名と生 JSON から適切な <see cref="IAction"/> インスタンスを生成して返す。</summary>
    /// <param name="eventName">GitHub Webhook の X-GitHub-Event ヘッダー値（小文字）。</param>
    /// <param name="rawJson">Webhook ペイロードの生 JSON 文字列。</param>
    /// <param name="webhookUrl">通知先 Discord Webhook URL。</param>
    /// <returns>イベントに対応する <see cref="IAction"/> インスタンス。</returns>
    IAction GetAction(string eventName, string rawJson, Uri webhookUrl);
}
```

- [ ] **Step 2: Rewrite `ActionFactory`**

Replace the entire content of `src/Actions/ActionFactory.cs`:

```csharp
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
```

- [ ] **Step 3: Rewrite `tests/ActionFactoryTests.cs`**

At this point, no actions have `[GitHubEvent]` yet (added in Task 8), so the registry is empty.

```csharp
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

    // ── Task 8 完了後に Registry.Count == 12 を検証するテストをここに追加する ──
    // Task 8 Step 7c 参照。
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionFactoryTests" -v n
```

Expected: 3 tests PASS.

- [ ] **Step 5: Add Captive Dependency guard comment to `Program.cs`**

In `src/Program.cs`, locate `.AddSingleton<IActionFactory, ActionFactory>()` and add the comment:

```csharp
// CAPTIVE DEPENDENCY GUARD: ActionFactory が受け取る IServiceProvider は root SP。
// Action の依存はすべて Singleton であること。
// Scoped サービスを Action に追加した場合は IServiceScopeFactory を使う設計に変更すること。
.AddSingleton<IActionFactory, ActionFactory>()
```

- [ ] **Step 6: Verify full test suite still passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

- [ ] **Step 7: Commit**

```bash
git add src/Actions/IActionFactory.cs src/Actions/ActionFactory.cs \
        tests/ActionFactoryTests.cs src/Program.cs
git commit -m "feat: ActionFactory をリフレクションレジストリに刷新・IActionFactory シグネチャ変更"
```

---

## Task 6: `ActionRegistryValidator`

**Files:**
- Create: `src/Services/ActionRegistryValidator.cs`
- Modify: `src/Program.cs`

**Interfaces:**
- Consumes: `ActionFactory.Registry` (Task 5)
- Produces: `ActionRegistryValidator` — `IHostedService` that validates every registered action at startup.

> **Note on startup timing**: Azure Functions Isolated Worker の `IHostedService.StartAsync` は Worker プロセス起動時に実行されるが、Functions Host（別プロセス）からのリクエストが競合して到達する可能性は極めて低い。`ValidateAll()` が同期的に例外をスローするため、失敗時は Worker プロセスが落ちてデプロイ失敗として安全に扱われる。

- [ ] **Step 1: Write the failing test**

Add to `tests/ActionFactoryTests.cs` (append inside the class):

```csharp
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
```

Add using:

```csharp
using GitHubWebhookBridge.Services;
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "ActionRegistryValidator_ValidateAll" -v n
```

- [ ] **Step 3: Implement**

Create `src/Services/ActionRegistryValidator.cs`:

```csharp
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
```

- [ ] **Step 4: Register in `Program.cs`**

Add after `AddSingleton<IActionFactory, ActionFactory>()`:

```csharp
using GitHubWebhookBridge.Services;
// ...
.AddHostedService<ActionRegistryValidator>()
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "ActionRegistryValidator_ValidateAll" -v n
```

Expected: 1 test PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Services/ActionRegistryValidator.cs src/Program.cs tests/ActionFactoryTests.cs
git commit -m "feat: ActionRegistryValidator を追加（起動時 DI 解決の検証）"
```

---

## Task 7: `WebhookFunction` Update

**Files:**
- Create: `src/Functions/WebhookEnvelope.cs`
- Modify: `src/Functions/WebhookFunction.cs`
- Modify: `tests/WebhookFunctionTests.cs`

**Interfaces:**
- Consumes: `IActionFactory.GetAction(string, string, Uri)` (Task 5), `OctokitJsonOptions` (Task 2)

> **Read `scratch/octokit-api-surface.md`**: `SenderIdType` が `int` の場合は `WebhookSender.Id` の型を `int` に変更すること。

- [ ] **Step 1: Create `WebhookEnvelope`**

Create `src/Functions/WebhookEnvelope.cs`:

```csharp
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// ミュートチェック用の最小ペイロード。送信者 ID と action フィールドのみ抽出する。
/// </summary>
internal sealed record WebhookEnvelope(
    [property: JsonPropertyName("sender")] WebhookSender? Sender,
    [property: JsonPropertyName("action")]  string?        Action);

/// <summary>送信者情報の最小表現。</summary>
internal sealed record WebhookSender(
    // scratch/octokit-api-surface.md の SenderIdType に合わせて long/int を選択すること
    [property: JsonPropertyName("id")] long? Id);
```

> **`Id` を `long?` にする理由**: `sender.id` が文字列など非数値だった場合に `JsonException` が発生して 500 になることを防ぐ。null の場合はミュートチェックをスキップする（従来と同じ動作）。

- [ ] **Step 2: Update `WebhookFunction.cs`**

**a) Remove the `_jsonOptions` static field** (was used only for `JsonElement` parse):

Delete:
```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    ReadCommentHandling = JsonCommentHandling.Skip,
};
```

**b) Replace step 7 (JSON parse to `JsonElement`)** — keep 400 for invalid JSON:

```csharp
// JSON 妥当性を維持しつつ raw string として保持する（400 レスポンスを維持するため）
string rawJson;
try
{
    rawJson = System.Text.Encoding.UTF8.GetString(rawBody);
    // 軽量バリデーション: JSON オブジェクトとして開始しているか確認する
    using var doc = JsonDocument.Parse(rawJson);
    if (doc.RootElement.ValueKind != JsonValueKind.Object)
        return new BadRequestObjectResult(new { message = "Bad Request: JSON body must be an object" });
}
catch (JsonException)
{
    return new BadRequestObjectResult(new { message = "Bad Request: Invalid JSON body" });
}
```

**c) Replace step 8 (mute check via `JsonElement`) with `WebhookEnvelope`**:

```csharp
// ミュートチェック（Id が null の場合はスキップ — 非数値 id への安全なフォールバック）
await _muteManager.EnsureLoadedAsync();
WebhookEnvelope? envelope = null;
try
{
    envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawJson, GitHubWebhookBridge.Utils.OctokitJsonOptions.Value);
}
catch (JsonException)
{
    // デシリアライズ失敗時はミュートチェックをスキップして処理を続行する
}
if (envelope?.Sender?.Id is { } senderId)
{
    if (_muteManager.IsMuted(senderId, eventName, envelope.Action))
        return new OkObjectResult(new { message = "Muted user" });
}
```

**d) Update step 9 (factory call)**:

```csharp
actionHandler = _actionFactory.GetAction(eventName, rawJson, webhookUrl);
```

**e) Add `using System.Text.Json;`** (for `JsonDocument.Parse`). Remove `JsonElement` usage if nothing else uses it.

- [ ] **Step 3: Update `WebhookFunctionTests.cs`**

**a) Update all mock setups** — search for `.GetAction(` and update:

```csharp
// Before: factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Uri>()))
// After:
factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
```

Also update `It.Is<JsonElement>(...)` matchers — if any, change to `It.Is<string>(s => ...)` with equivalent string content checks.

**b) Add tests for the new mute check behavior**:

```csharp
[Fact]
public async Task RunAsync_SenderIdIsString_SkipsMuteCheckAndContinues()
{
    // sender.id が文字列のとき WebhookEnvelope が null Id を返し、ミュートチェックをスキップする
    Mock<IActionFactory> factory = new();
    Mock<IAction> action = new();
    action.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);
    factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
           .Returns(action.Object);

    var req = BuildRequest(
        body: """{"sender":{"id":"not-a-number"},"action":"opened"}""",
        secret: TestSecret,
        eventName: "push");

    var fn = CreateFunction(factoryMock: factory);
    var result = await fn.RunAsync(req);

    Assert.IsType<OkResult>(result); // クラッシュせず 200 を返す
}

[Fact]
public async Task RunAsync_SenderFieldMissing_SkipsMuteCheckAndContinues()
{
    Mock<IActionFactory> factory = new();
    Mock<IAction> action = new();
    action.Setup(a => a.RunAsync()).Returns(Task.CompletedTask);
    factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
           .Returns(action.Object);

    var req = BuildRequest(
        body: """{"action":"opened"}""",
        secret: TestSecret,
        eventName: "push");

    var fn = CreateFunction(factoryMock: factory);
    var result = await fn.RunAsync(req);

    Assert.IsType<OkResult>(result);
}
```

**c) Verify existing invalid JSON test still returns 400**:

The test `RunAsync_InvalidJsonReturns400` (or equivalent) must still pass. Run it explicitly:

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "InvalidJson" -v n
```

Expected: PASS (400 is preserved by `JsonDocument.Parse` validation).

- [ ] **Step 4: Run all WebhookFunction tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "WebhookFunctionTests" -v n
```

Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Functions/WebhookEnvelope.cs src/Functions/WebhookFunction.cs \
        tests/WebhookFunctionTests.cs
git commit -m "feat: WebhookFunction を raw JSON 渡しに変更・WebhookEnvelope でミュートチェック（400 維持）"
```

---

## Task 8: Migrate 12 Implemented Actions to Octokit Types

**Files:**
- Modify: `src/Actions/BaseAction.cs`
- Modify: `src/Actions/Impl/*.cs` (12 files)
- Modify: `tests/*ActionTests.cs` (12 files)
- Modify: `tests/MonkeyTests.cs`

**Interfaces:**
- Consumes: `GitHubEventAttribute` (Task 3), Octokit types (Task 1)

> **REQUIRED: Read `scratch/octokit-api-surface.md` before any code in this task.**
> The file determines:
> - Level 1 vs Level 2 for `[GitHubEvent]`
> - Exact Octokit type names and namespaces
> - Whether `Action` is `string` or `enum`
> - Whether `HtmlUrl` is `Uri` or `string`

> **Constructor convention for `ActivatorUtilities.CreateInstance`**:
> ```
> (IDiscordClient, IMessageCacheService, IGitHubUserMapManager, ILogger<TAction>,
>  Uri webhookUrl, string eventName, TOctokitPayload payload)
> ```
> DI-resolved params first, runtime params after. `Uri`, `string`, `TOctokitPayload` are all unique in the params array — no type collision.

- [ ] **Step 1: Add type constraint to `BaseAction<T>`**

Edit `src/Actions/BaseAction.cs` — change the class declaration:

```csharp
using Octokit.Webhooks;
// ...
public abstract class BaseAction<TEvent>(
    // ...same params as before...
) : IAction where TEvent : WebhookEvent
```

- [ ] **Step 2: Verify build fails (expected)**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release 2>&1 | grep -c "error CS"
```

Expected: errors about hand-written model types not satisfying the `WebhookEvent` constraint.

- [ ] **Step 3a: Commit type constraint alone**

```bash
git add src/Actions/BaseAction.cs
git commit -m "feat: BaseAction<T> に Octokit.Webhooks.WebhookEvent 型制約を追加"
```

- [ ] **Step 4: Migrate the 12 action source files**

> **Read `scratch/octokit-api-surface.md`** for exact type names. The table below uses the most likely names — verify each against the API surface file and correct where needed.

| Action | Hand-written type | Expected Octokit type |
|---|---|---|
| `PingAction` | `PingEvent` | `Octokit.Webhooks.Events.PingEvent` |
| `PushAction` | `PushEvent` | `Octokit.Webhooks.Events.PushEvent` |
| `StarAction` | `StarEvent` | `Octokit.Webhooks.Events.StarEvent` |
| `ForkAction` | `ForkEvent` | `Octokit.Webhooks.Events.ForkEvent` |
| `PublicAction` | `PublicEvent` | `Octokit.Webhooks.Events.PublicEvent` |
| `IssuesAction` | `IssuesEvent` | `Octokit.Webhooks.Events.IssuesEvent` |
| `IssueCommentAction` | `IssueCommentEvent` | `Octokit.Webhooks.Events.IssueCommentEvent` |
| `DiscussionAction` | `DiscussionEvent` | `Octokit.Webhooks.Events.DiscussionEvent` |
| `PullRequestAction` | `PullRequestEvent` | `Octokit.Webhooks.Events.PullRequestEvent` |
| `PullRequestReviewAction` | `PullRequestReviewEvent` | `Octokit.Webhooks.Events.PullRequestReviewEvent` |
| `PullRequestReviewCommentAction` | `PullRequestReviewCommentEvent` | `Octokit.Webhooks.Events.PullRequestReviewCommentEvent` |
| `PullRequestReviewThreadAction` | `PullRequestReviewThreadEvent` | `Octokit.Webhooks.Events.PullRequestReviewThreadEvent` |

For each action, apply this pattern (shown with `PullRequestAction`):

```csharp
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
// If Action is enum: using Octokit.Webhooks.Events.PullRequest;
// Determined by scratch/octokit-api-surface.md ActionType field

// [GitHubEvent(WebhookEventType.PullRequest)]   ← Level 1
// [GitHubEvent]                                  ← Level 2 (if Level2Available: true)
[GitHubEvent(/* see octokit-api-surface.md */)]
public sealed class PullRequestAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestEvent pullRequestEvent)             // ← Octokit type
    : BaseAction<PullRequestEvent>(discord, webhookUrl, eventName, pullRequestEvent, cache, userMapManager, logger)
```

Remove `using GitHubWebhookBridge.Models.GitHubWebhooks;`.

**Property name conflicts**: run `dotnet build` after each file. Fix compiler errors only — do not use string guessing. If a property name changed (e.g., `HtmlUrl` type changed from `string` to `Uri`), adapt the `RunAsync()` code accordingly. The `scratch/octokit-api-surface.md` `HtmlUrlType` field tells you which type to expect.

- [ ] **Step 5: Verify build succeeds**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 6a: Commit source migration**

```bash
git add src/Actions/Impl/
git commit -m "feat: 12 アクションを Octokit.Webhooks 型に移行"
```

- [ ] **Step 7: Update `tests/*ActionTests.cs` (12 files)**

For each test file:
1. Remove `using GitHubWebhookBridge.Models.GitHubWebhooks;`
2. Add Octokit namespaces (from `octokit-api-surface.md`)
3. Update helper methods to construct Octokit types
4. Update action constructor calls to the new signature

**Pattern (PullRequestActionTests.cs):**

```csharp
// Remove: using GitHubWebhookBridge.Models.GitHubWebhooks;
// Add:
using Octokit.Webhooks.Events;
// If Action is enum: using Octokit.Webhooks.Events.PullRequest;

// Update MakePrEvent — property names from octokit-api-surface.md:
private static PullRequestEvent MakePrEvent(string action, bool merged = false, bool draft = false)
{
    // Construct using Octokit types. Run dotnet build and fix each property error.
    // HtmlUrl is Uri or string (see octokit-api-surface.md HtmlUrlType).
    // Action is string or enum (see octokit-api-surface.md ActionType).
    throw new NotImplementedException("Fill this in after reading octokit-api-surface.md");
}

// Update action construction:
PullRequestAction action = new(
    discord.Object, cache.Object, userMap.Object,
    Mock.Of<ILogger<PullRequestAction>>(),
    _webhookUri, "pull_request",
    MakePrEvent("opened"));
```

> **Note on `ILogger<T>` vs `ILogger`**: `Mock.Of<ILogger<PullRequestAction>>()` returns `ILogger<PullRequestAction>` which satisfies `ILogger<PullRequestAction>`. Do NOT use `Mock.Of<ILogger>()` (non-generic) as it will fail `ActivatorUtilities` type matching.

- [ ] **Step 8: Update `tests/MonkeyTests.cs`**

**a)** Remove:
```csharp
using GitHubWebhookBridge.Actions.Stubs;
using GitHubWebhookBridge.Models.GitHubWebhooks;
```

**b)** Add Octokit namespaces.

**c)** Update every direct action constructor call in `MonkeyTests.cs` to the new signature (same DI-first ordering as above). MonkeyTests creates actions directly — find all `new PingAction(...)`, `new PushAction(...)` etc. and update each one.

- [ ] **Step 9: Run all action tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "ActionTests|DiscussionActionTests|ForkActionTests|IssueCommentActionTests|IssuesActionTests|PingActionTests|PublicActionTests|PullRequestActionTests|PullRequestReviewActionTests|PullRequestReviewCommentActionTests|PullRequestReviewThreadActionTests|PushActionTests|StarActionTests|MonkeyTests" -v n
```

Expected: all PASS.

- [ ] **Step 10: Update `tests/ActionFactoryTests.cs` — add full registry + E2E tests**

Add these tests to `ActionFactoryTests`. First build a `ServiceProvider` with all required DI mocks:

```csharp
private static IServiceProvider BuildFullServiceProvider()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(Mock.Of<IDiscordClient>());
    services.AddSingleton(Mock.Of<IMessageCacheService>());
    services.AddSingleton(Mock.Of<IGitHubUserMapManager>());
    return services.BuildServiceProvider();
}

[Fact]
public void Registry_Contains12Events()
{
    var factory = new ActionFactory(BuildFullServiceProvider());
    Assert.Equal(12, factory.Registry.Count);
}

[Theory]
[InlineData("ping")]
[InlineData("push")]
[InlineData("star")]
[InlineData("fork")]
[InlineData("public")]
[InlineData("issues")]
[InlineData("issue_comment")]
[InlineData("discussion")]
[InlineData("pull_request")]
[InlineData("pull_request_review")]
[InlineData("pull_request_review_comment")]
[InlineData("pull_request_review_thread")]
public void GetAction_KnownEvent_IsRegistered(string eventName)
{
    var factory = new ActionFactory(BuildFullServiceProvider());
    Assert.True(factory.Registry.ContainsKey(eventName),
        $"Event '{eventName}' is not registered. Did you add [GitHubEvent] to the action class?");
}

// E2E: GetAction を実際に呼び出して正しい型のインスタンスが返ることを検証する
[Theory]
[InlineData("ping",    """{"zen":"ok","hook_id":1,"hook":{"type":"Repository"}}""",                typeof(PingAction))]
[InlineData("push",    """{"ref":"refs/heads/main","commits":[],"pusher":{"name":"u","email":"u@e"},"repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""", typeof(PushAction))]
[InlineData("star",    """{"action":"created","repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""", typeof(StarAction))]
public void GetAction_KnownEvent_ReturnsCorrectType(string eventName, string json, Type expectedType)
{
    var factory = new ActionFactory(BuildFullServiceProvider());
    var action = factory.GetAction(eventName, json, _webhookUri);
    Assert.IsType(expectedType, action);
}
```

Add usings for the action types:
```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Moq;
```

- [ ] **Step 11: Run updated ActionFactory tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionFactoryTests" -v n
```

Expected: all PASS.

- [ ] **Step 12: Update `ActionRegistryValidator` test with full DI**

In `tests/ActionFactoryTests.cs`, update `ActionRegistryValidator_ValidateAll_DoesNotThrowForEmptyRegistry` or add a new test:

```csharp
[Fact]
public void ActionRegistryValidator_ValidateAll_DoesNotThrowForFullRegistry()
{
    var sp = BuildFullServiceProvider();
    var factory = new ActionFactory(sp);
    var validator = new ActionRegistryValidator(factory, sp);

    var ex = Record.Exception(() => validator.ValidateAll());
    Assert.Null(ex);
}
```

- [ ] **Step 13: Update `ActionCoverageTests.cs`**

Switch from namespace-based to `[GitHubEvent]`-based detection so actions placed outside `Actions.Impl` are also caught:

```csharp
[Fact]
public void AllGitHubEventAnnotatedActionsHaveTestClass()
{
    Assembly mainAssembly = typeof(IAction).Assembly;
    Type[] implementedActions = mainAssembly.GetTypes()
        .Where(t =>
            t.GetCustomAttribute<GitHubEventAttribute>() != null &&
            t.IsClass &&
            !t.IsAbstract)
        .ToArray();

    Assembly testAssembly = typeof(ActionCoverageTests).Assembly;
    HashSet<string> testClassNames = testAssembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests", StringComparison.Ordinal))
        .Select(t => t.Name)
        .ToHashSet();

    List<string> uncovered = implementedActions
        .Where(a => !testClassNames.Contains($"{a.Name}Tests"))
        .Select(a => a.Name)
        .OrderBy(n => n)
        .ToList();

    Assert.True(
        uncovered.Count == 0,
        $"以下の実装済みアクションにテストクラスがありません:{Environment.NewLine}" +
        string.Join(Environment.NewLine, uncovered.Select(n => $"  - {n} → {n}Tests.cs が必要")));
}
```

- [ ] **Step 14: Run full test suite**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

Expected: all PASS.

- [ ] **Step 15: Commit test migration**

```bash
git add tests/
git commit -m "test: アクションテストを Octokit.Webhooks 型に移行・ActionFactory E2E テスト追加"
```

---

## Task 9: Delete Obsolete Files

**Files:**
- Delete: `src/Actions/Stubs/StubActions.cs`
- Delete: `src/Models/GitHubWebhooks/*.cs` (13 files)
- Delete: `src/Models/GitHubWebhooks/Generated/` (entire directory)
- Modify: `src/GitHubWebhookBridge.csproj` (remove `<Compile Remove>`)

**Interfaces:**
- Consumes: all previous tasks (safe to delete only after Task 8 is fully green)

- [ ] **Step 1: Delete obsolete source files**

```bash
rm src/Actions/Stubs/StubActions.cs
rm src/Models/GitHubWebhooks/Common.cs
rm src/Models/GitHubWebhooks/DiscussionEvent.cs
rm src/Models/GitHubWebhooks/ForkEvent.cs
rm src/Models/GitHubWebhooks/IssueCommentEvent.cs
rm src/Models/GitHubWebhooks/IssuesEvent.cs
rm src/Models/GitHubWebhooks/PingEvent.cs
rm src/Models/GitHubWebhooks/PublicEvent.cs
rm src/Models/GitHubWebhooks/PullRequestEvent.cs
rm src/Models/GitHubWebhooks/PullRequestReviewCommentEvent.cs
rm src/Models/GitHubWebhooks/PullRequestReviewEvent.cs
rm src/Models/GitHubWebhooks/PullRequestReviewThreadEvent.cs
rm src/Models/GitHubWebhooks/PushEvent.cs
rm src/Models/GitHubWebhooks/StarEvent.cs
rm -rf src/Models/GitHubWebhooks/Generated/
rmdir src/Models/GitHubWebhooks 2>/dev/null || true
rmdir src/Models 2>/dev/null || true
```

- [ ] **Step 2: Update `GitHubWebhookBridge.csproj`**

Remove the entire `<Compile Remove>` item group:

```xml
<!-- 削除する ItemGroup:
<ItemGroup>
  <Compile Remove="Models/GitHubWebhooks/Generated/**" />
</ItemGroup>
-->
```

- [ ] **Step 3: Verify build and tests**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release && \
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

Expected: 0 warnings, all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: StubActions・手書きモデル・生成モデルを削除"
```

---

## Task 10: `OctokitWebhooksCompatibilityTests`

**Files:**
- Create: `tests/OctokitWebhooksCompatibilityTests.cs`
- Create: `tests/Snapshots/octokit-known-events.txt` (generated, committed)
- Modify: `tests/GitHubWebhookBridge.Tests.csproj`

**Interfaces:**
- Consumes: `Octokit.Webhooks.WebhookEvent` base class

> **Read `scratch/octokit-api-surface.md`** — use `Level2Available` to determine how to extract event names in `GetEventName()`. Copy the full event type list from the file into `octokit-known-events.txt`.

- [ ] **Step 1: Generate the known-events file**

Create a temporary fact to print all event names, run it, and capture to file:

```csharp
// Paste into a temporary test, run, copy output, delete
[Fact]
public void PrintAllEventNames()
{
    var names = typeof(Octokit.Webhooks.WebhookEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(Octokit.Webhooks.WebhookEvent)) && !t.IsAbstract)
        .Select(GetEventName)
        .OrderBy(n => n);
    foreach (var n in names) _output.WriteLine(n);
}
```

Run:

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "PrintAllEventNames" -v n 2>&1 | grep "^    " | tr -d ' ' \
  > tests/Snapshots/octokit-known-events.txt
```

Verify the file is non-empty and contains event names like `pull_request`, `push`, etc.

- [ ] **Step 2: Write the compatibility test**

Create `tests/OctokitWebhooksCompatibilityTests.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Octokit.Webhooks のトップレベルイベント型が既知リストと一致することを検証する。
/// Renovate による NuGet 更新後にこのテストが落ちた場合:
///   1. UPDATE_KNOWN_EVENTS=1 dotnet test --filter OctokitWebhooksCompatibilityTests を実行
///   2. tests/Snapshots/octokit-known-events.txt の差分を確認
///   3. 新イベントは実装 or コメントで「意図的に未実装」を記録してからコミット
/// </summary>
public class OctokitWebhooksCompatibilityTests
{
    private static readonly string KnownEventsPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Snapshots",
        "octokit-known-events.txt");

    [Fact]
    public void OctokitWebhookEventTypes_MustMatchKnownList()
    {
        var known = File.ReadAllLines(KnownEventsPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var actual = typeof(WebhookEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
            .Select(GetEventName)
            .ToHashSet(StringComparer.Ordinal);

        if (Environment.GetEnvironmentVariable("UPDATE_KNOWN_EVENTS") == "1")
        {
            File.WriteAllLines(KnownEventsPath, actual.OrderBy(n => n));
            return;
        }

        var added   = actual.Except(known).OrderBy(n => n).ToList();
        var removed = known.Except(actual).OrderBy(n => n).ToList();

        Assert.True(added.Count == 0,
            $"Octokit.Webhooks に新しいイベント型が追加されました。実装するか既知リストに追記してください: {string.Join(", ", added)}");
        Assert.True(removed.Count == 0,
            $"Octokit.Webhooks からイベント型が削除されました。既知リストから除去してください: {string.Join(", ", removed)}");
    }

    /// <summary>
    /// イベント型からイベント名を取得する。
    /// scratch/octokit-api-surface.md の Level2Available に応じて実装を選択すること。
    /// </summary>
    private static string GetEventName(Type t)
    {
        // Level 2 の場合 (Level2Available: true):
        // var attr = t.GetCustomAttribute<LEVEL2_ATTRIBUTE_TYPE>();
        // if (attr != null) return attr.LEVEL2_PROPERTY_NAME;

        // Level 2 不可の場合: 型名 → snake_case 変換
        var name = t.Name;
        if (name.EndsWith("Event", StringComparison.Ordinal))
            name = name[..^5];
        if (name.EndsWith("Webhook", StringComparison.Ordinal))
            name = name[..^7];
        return Regex.Replace(name, "([a-z0-9])([A-Z])|([A-Z]+)([A-Z][a-z])", "$1$3_$2$4")
                    .ToLowerInvariant();
    }
}
```

- [ ] **Step 3: Add snapshot to test project output**

Edit `tests/GitHubWebhookBridge.Tests.csproj` — add inside an `<ItemGroup>`:

```xml
<ItemGroup>
  <None Update="Snapshots/**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "OctokitWebhooksCompatibilityTests" -v n
```

Expected: 1 test PASS.

- [ ] **Step 5: Commit**

```bash
git add tests/OctokitWebhooksCompatibilityTests.cs \
        tests/Snapshots/octokit-known-events.txt \
        tests/GitHubWebhookBridge.Tests.csproj
git commit -m "test: Octokit.Webhooks イベント型互換性テストを追加"
```

---

## Task 11: `OctokitPayloadSchemaSnapshotTests`

**Files:**
- Create: `tests/OctokitPayloadSchemaSnapshotTests.cs`
- Create: `tests/Snapshots/octokit-payload-schema.json` (generated, committed)

**Interfaces:**
- Consumes: `GitHubEventAttribute` (Task 3), Octokit types (Task 1)

- [ ] **Step 1: Write the test**

Create `tests/OctokitPayloadSchemaSnapshotTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubWebhookBridge.Actions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// 実装済みアクションのペイロード型スキーマが変化したことを検知するスナップショットテスト。
/// Renovate による Octokit.Webhooks 更新後にこのテストが落ちた場合:
///   UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests
///   を実行してスナップショットを更新し、差分をレビューしてからコミットすること。
/// </summary>
public class OctokitPayloadSchemaSnapshotTests
{
    private static readonly string SnapshotPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Snapshots",
        "octokit-payload-schema.json");

    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    [Fact]
    public void ImplementedPayloadTypes_MustMatchSnapshot()
    {
        var schema = BuildSchema();
        var actual = JsonSerializer.Serialize(schema, PrettyOptions);

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            File.WriteAllText(SnapshotPath, actual);
            return;
        }

        Assert.True(File.Exists(SnapshotPath),
            $"Snapshot file not found at {SnapshotPath}. " +
            $"Run: UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests");

        var expected = File.ReadAllText(SnapshotPath);
        Assert.True(expected == actual,
            $"Octokit.Webhooks モデルスキーマが変化しました。" +
            $"UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests を実行して差分を確認してください。");
    }

    private static SortedDictionary<string, object> BuildSchema()
    {
        var payloadTypes = typeof(GitHubEventAttribute).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(GetPayloadType)
            .Distinct()
            .OrderBy(t => t.Name);

        var schema = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var type in payloadTypes)
            schema[type.Name] = BuildTypeSchema(type, new HashSet<Type>());

        return schema;
    }

    private static object BuildTypeSchema(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return "«circular»";

        var props = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .OrderBy(p => p.Name))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (propType.IsEnum)
            {
                // Enum: メンバー名と基底値をスナップショット化する（追加・削除・変更を検知）
                props[jsonName] = new
                {
                    type    = "enum:" + propType.Name,
                    members = Enum.GetNames(propType)
                                  .Zip(Enum.GetValues(propType).Cast<int>(),
                                       (n, v) => $"{n}={v}")
                                  .OrderBy(s => s)
                                  .ToArray(),
                };
            }
            else if (propType.IsClass
                     && propType != typeof(string)
                     && propType != typeof(Uri)
                     && !propType.IsArray
                     && !(propType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(propType))
                     && propType.Namespace?.StartsWith("Octokit", StringComparison.Ordinal) == true)
            {
                // ネストした Octokit 型: 再帰的にスキーマを構築する
                props[jsonName] = BuildTypeSchema(propType, new HashSet<Type>(visited));
            }
            else if (propType.IsGenericType
                     && typeof(System.Collections.IEnumerable).IsAssignableFrom(propType))
            {
                var elemType = propType.GetGenericArguments().FirstOrDefault();
                props[jsonName] = "array:" + (elemType?.Name ?? "object");
            }
            else if (propType.IsArray)
            {
                props[jsonName] = "array:" + (propType.GetElementType()?.Name ?? "object");
            }
            else
            {
                props[jsonName] = propType.Name;
            }
        }

        return props;
    }

    private static Type GetPayloadType(Type actionType)
    {
        var b = actionType.BaseType;
        while (b != null && !(b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BaseAction<>)))
            b = b.BaseType;
        return b?.GetGenericArguments()[0]
               ?? throw new InvalidOperationException($"Cannot find payload type for {actionType.Name}");
    }
}
```

- [ ] **Step 2: Generate the initial snapshot**

> **⚠ WARNING**: Do NOT commit or push before completing this step. CI will fail if the snapshot file does not exist in the repository.

Run with `UPDATE_SNAPSHOTS=1` to generate the file into the test output directory:

```bash
UPDATE_SNAPSHOTS=1 dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "OctokitPayloadSchemaSnapshotTests" -v n
```

Copy the generated file to the source tree (use `find` to avoid hardcoding the TFM):

```bash
find tests/bin -name "octokit-payload-schema.json" -print -quit \
  | xargs -I{} cp {} tests/Snapshots/octokit-payload-schema.json
```

Verify the file exists and is non-empty:

```bash
test -s tests/Snapshots/octokit-payload-schema.json && echo "OK" || echo "MISSING"
```

- [ ] **Step 3: Run test to verify snapshot matches**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release \
  --filter "OctokitPayloadSchemaSnapshotTests" -v n
```

Expected: 1 test PASS.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

Expected: all tests PASS, line coverage ≥ 80%, mutation score ≥ 60%.

- [ ] **Step 5: Commit**

> Confirm `tests/Snapshots/octokit-payload-schema.json` is included in the commit before pushing.

```bash
git add tests/OctokitPayloadSchemaSnapshotTests.cs \
        tests/Snapshots/octokit-payload-schema.json
git commit -m "test: Octokit.Webhooks ペイロードスキーマスナップショットテストを追加"
```

---

## Self-Review

### Spec Coverage

| Spec requirement | Task |
|---|---|
| Reflection-based ActionFactory (no switch) | Task 5 |
| `[GitHubEvent]` attribute, Level 1 + Level 2 | Task 3, Task 8 |
| Octokit.Webhooks model types | Task 1, Task 8 |
| STJ lenient deserialization (`UnmappedMemberHandling.Skip`) | Task 2 |
| `UnhandledAction` → 406 | Task 4 |
| `ActionRegistryValidator` (startup validation, `{}` deserialization safety) | Task 6 |
| `WebhookFunction` raw string + `WebhookEnvelope` mute check | Task 7 |
| Invalid JSON still returns 400 (not 500) | Task 7 |
| `sender.id` non-numeric safe handling | Task 7 (`long?` + try-catch) |
| Delete StubActions, hand-written models | Task 9 |
| `MonkeyTests` constructor update | Task 8 Step 8 |
| Octokit event-type compatibility test (file-based, not hardcoded) | Task 10 |
| Payload schema snapshot test (recursive, includes enums) | Task 11 |
| E2E test: `GetAction` actual invocation | Task 8 Step 10 |
| `JsonSerializerOptions.MakeReadOnly()` | Task 2 |
| `typeof(ActionFactory).Assembly` (not `GetExecutingAssembly`) | Task 5 |
| Case-sensitive `FrozenDictionary` (noted) | Task 5 |
| `Octokit.Webhooks` pinned version (not `*`) | Task 1 |
| Level 1/2 API surface documented and referenced by downstream tasks | Task 1 (`scratch/octokit-api-surface.md`) |
| Captive Dependency guard documented | Task 5 |
| Snapshot copy uses `find` (not hardcoded TFM) | Task 11 |
| `ActionCoverageTests` switched to `[GitHubEvent]`-based | Task 8 Step 13 |
| Task 8 split into 3 commits | Task 8 Steps 3a, 6a, 15 |

### Placeholder Scan

No "TBD", "similar to", or purely descriptive steps without code. Task 8 Step 4 table uses "Expected Octokit type" with a note to verify against `scratch/octokit-api-surface.md` — this is intentional and safe because Task 1 produces that file first.

### Type Consistency

- `IActionFactory.GetAction(string, string, Uri)` — consistent across Tasks 5, 7, WebhookFunctionTests.
- `ActionFactory(IServiceProvider sp)` — consistent across Tasks 5, 6.
- `UnhandledAction(string eventName)` — consistent across Tasks 4, 5.
- `OctokitJsonOptions.Value` — consistent across Tasks 2, 5, 6, 7, 11.
- `GetPayloadType()` using `typeof(BaseAction<>)` — consistent across Tasks 5, 6, 11.
- `long?` for `WebhookSender.Id` — consistent with Task 7 mute check pattern.
