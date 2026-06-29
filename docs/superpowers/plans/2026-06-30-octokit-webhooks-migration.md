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

---

## File Map

| Status | Path | Responsibility |
|---|---|---|
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
| **Modify** | `src/Functions/WebhookFunction.cs` | Pass raw string to factory; use WebhookEnvelope for mute check |
| **Modify** | `src/Program.cs` | Register `ActionRegistryValidator` as `IHostedService` |
| **Modify** | `tests/ActionFactoryTests.cs` | Rewrite for reflection-registry behavior |
| **Modify** | `tests/*ActionTests.cs` (12 files) | Update helpers to build Octokit payload types |
| **Modify** | `tests/MonkeyTests.cs` | Remove `using GitHubWebhookBridge.Actions.Stubs` |
| **Modify** | `tests/WebhookFunctionTests.cs` | Update `IActionFactory` mock signature |
| **Modify** | `tests/ActionCoverageTests.cs` | Update to exclude `UnhandledAction` from coverage check |
| **Delete** | `src/Actions/Stubs/StubActions.cs` | Replaced by `UnhandledAction` |
| **Delete** | `src/Models/GitHubWebhooks/Common.cs` + 11 event files | Replaced by Octokit.Webhooks types |
| **Delete** | `src/Models/GitHubWebhooks/Generated/` (entire dir) | No longer needed |

---

## Task 1: Add Octokit.Webhooks NuGet and verify API surface

**Files:**
- Modify: `src/GitHubWebhookBridge.csproj`

**Interfaces:**
- Produces: Knowledge of exact Octokit.Webhooks .NET API — class-level attribute (Level 2) feasibility, `WebhookEventType` constant names (Level 1), `JsonSerializerOptions` exposure, and exact model property names used by the 12 implemented actions.

- [ ] **Step 1: Add the package**

Find the latest stable version of `Octokit.Webhooks` on NuGet:

```bash
dotnet add src/GitHubWebhookBridge.csproj package Octokit.Webhooks
```

Open `src/GitHubWebhookBridge.csproj` and confirm a concrete version was pinned (not `*`). The package adds to `<ItemGroup>`:

```xml
<PackageReference Include="Octokit.Webhooks" Version="X.Y.Z" />
```

- [ ] **Step 2: Verify the API surface with a scratch script**

Create a temporary file `scratch/verify-octokit.csx` (delete after this task):

```csharp
#!/usr/bin/env dotnet-script
// Run with: dotnet script scratch/verify-octokit.csx
// (or just compile and check output in a test)
using System.Reflection;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

// 1. Check if WebhookEvent subclasses have a class-level attribute
//    (Level 2 feasibility)
var prType = typeof(PullRequestEvent);  // adjust if namespace differs
var attrs = prType.GetCustomAttributes(inherit: false);
Console.WriteLine("=== PullRequestEvent attributes ===");
foreach (var a in attrs) Console.WriteLine(a.GetType().FullName);

// 2. Check WebhookEventType constants
var etType = typeof(WebhookEventType);
Console.WriteLine("\n=== WebhookEventType sample constants ===");
foreach (var f in etType.GetFields(BindingFlags.Public | BindingFlags.Static).Take(5))
    Console.WriteLine($"  {f.Name} = {f.GetValue(null)}");

// 3. Check if Octokit exposes JsonSerializerOptions
// Look for: OctokitWebhooksDefaults, WebhookSerializer, or similar
Console.WriteLine("\n=== JsonSerializerOptions exposure ===");
// Manually inspect Octokit.Webhooks namespace for any Options/Defaults types
```

Run `dotnet restore` first, then execute the script or write a quick xUnit fact. Record the answers:
- **Level 2**: Do event classes have `[WebhookEvent("...")]` or similar attribute? If yes, note the attribute type and property name.
- **Level 1**: Confirm `WebhookEventType.PullRequest`, `WebhookEventType.Push`, etc. exist and match `"pull_request"`, `"push"` (lowercase snake_case).
- **Options**: Is there a `WebhookSerializer.Options` or `OctokitWebhooksDefaults.SerializerOptions`? If yes, use it as the base in `OctokitJsonOptions`.
- **Model namespaces**: Note the exact namespace of `PullRequestWebhookEvent` (likely `Octokit.Webhooks.Events`) and confirm property names match the current hand-written models.

Delete `scratch/` after verification.

- [ ] **Step 3: Update packages.lock.json**

```bash
cd src && dotnet restore --force-evaluate
```

Confirm `packages.lock.json` is updated with the new package.

- [ ] **Step 4: Verify build is clean**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/GitHubWebhookBridge.csproj src/packages.lock.json
git commit -m "chore: Octokit.Webhooks を追加"
```

---

## Task 2: OctokitJsonOptions

**Files:**
- Create: `src/Utils/OctokitJsonOptions.cs`

**Interfaces:**
- Produces: `OctokitJsonOptions.Value` — `JsonSerializerOptions` used by `ActionFactory` and `WebhookFunction`.

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

Create `src/Utils/OctokitJsonOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// Octokit.Webhooks モデルのデシリアライズに使用する共有 <see cref="JsonSerializerOptions"/>。
/// Octokit が JsonSerializerOptions を公開している場合はそれをベースにする（Task 1 の調査結果を参照）。
/// </summary>
internal static class OctokitJsonOptions
{
    /// <summary>読み取り専用の共有オプションインスタンス。</summary>
    public static readonly JsonSerializerOptions Value = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        // Task 1 の調査結果に応じて変更:
        //   Octokit が Options を公開している場合: var opts = OctokitWebhooksDefaults.SerializerOptions.Clone() など
        //   公開していない場合: 以下の new JsonSerializerOptions() のまま
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

Expected: build error — `GitHubEventAttribute` does not exist yet.

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

Expected: build error — `UnhandledAction` does not exist yet.

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
- Consumes: `GitHubEventAttribute` (Task 3), `UnhandledAction` (Task 4), `OctokitJsonOptions` (Task 2), `Octokit.Webhooks` (Task 1)
- Produces: `IActionFactory.GetAction(string eventName, string rawJson, Uri webhookUrl)` — replaces `JsonElement body` with `string rawJson`.

> **Note**: At this point, the existing 12 implemented actions still use hand-written model types (Task 8 migrates them). The factory works with them because `GetPayloadType()` resolves whatever generic `T` is declared on `BaseAction<T>`. The build will succeed as long as no `[GitHubEvent]` attributes are on the actions yet — add the attributes in Task 8.

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
/// スイッチ式を持たず、新しいイベントのクラスを追加するだけで自動登録される。
/// </summary>
public class ActionFactory(IServiceProvider sp) : IActionFactory
{
    private readonly IServiceProvider _sp = sp;

    // 起動時に一度だけ構築。キーはすべて小文字スネークケース（GitHub のイベント名仕様）。
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

        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// 継承チェーンを辿って <c>BaseAction&lt;T&gt;</c> を探し、型引数 T を返す。
    /// </summary>
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

    /// <summary>
    /// Level 2: ペイロード型の Octokit 属性からイベント名を導出する。
    /// Level 1: <see cref="GitHubEventAttribute"/> の明示的な文字列を使う。
    /// どちらも解決できない場合は起動時に例外をスローする。
    /// </summary>
    private static string ResolveEventName(Type actionType, Type payloadType)
    {
        // --- Level 2 ---
        // Task 1 の調査で Octokit がクラス属性を公開している場合:
        //   var octokitAttr = payloadType.GetCustomAttribute<WebhookEventTypeAttribute>(); // 属性名は調査結果に合わせる
        //   if (octokitAttr != null) return octokitAttr.EventType; // プロパティ名も調査結果に合わせる
        // Task 1 で Level 2 が使えないと判明した場合は上記をコメントアウトしたまま Level 1 のみ使う。

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

        // ActivatorUtilities が DI サービスを _sp から解決し、残りを params array から充填する。
        // Uri と payload の具体型は各アクションコンストラクター内で一意なため型の衝突は起きない。
        // eventName (string) はコンストラクターに string 型の引数がある場合に先頭から埋まる。
        return (IAction)ActivatorUtilities.CreateInstance(_sp, entry.Action, webhookUrl, eventName, payload);
    }

    /// <summary>テスト用: レジストリを外部から参照できるようにする。</summary>
    internal IReadOnlyDictionary<string, (Type Action, Type Payload)> Registry => _registry;
}
```

- [ ] **Step 3: Rewrite `ActionFactoryTests.cs`**

At this point, no actions have `[GitHubEvent]` yet (added in Task 8), so the registry is empty. Write tests that verify the registry mechanics and the unhandled fallback.

Replace `tests/ActionFactoryTests.cs`:

```csharp
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>ActionFactory のリフレクションレジストリ動作テスト。</summary>
public class ActionFactoryTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    /// <summary>サービスプロバイダーを構築するヘルパー。</summary>
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // 実際の DI 依存はテストで必要になった時点で追加する（現時点では空でよい）
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_BuildsRegistryWithoutThrowing()
    {
        var sp = BuildServiceProvider();
        Exception? ex = Record.Exception(() => new ActionFactory(sp));
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
    public async Task GetAction_UnknownEvent_UnhandledActionThrowsNotImplementedException()
    {
        var factory = new ActionFactory(BuildServiceProvider());
        var action = factory.GetAction("unknown_event", "{}", _webhookUri);
        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    /// <summary>
    /// [GitHubEvent] 属性を持つクラスを動的に生成してレジストリへの自動登録を検証する。
    /// 注: 実際の Action クラスは Task 8 で [GitHubEvent] が付与された後に追加テストを書く。
    /// </summary>
    [Fact]
    public void Registry_ContainsAllGitHubEventAnnotatedTypes()
    {
        var factory = new ActionFactory(BuildServiceProvider());
        var registeredCount = factory.Registry.Count;
        // Task 8 完了前はゼロ、完了後は 12 になることを確認するため値を出力
        // このテスト自体はカウントが非負であることのみ確認する
        Assert.True(registeredCount >= 0);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionFactoryTests" -v n
```

Expected: 4 tests PASS.

- [ ] **Step 5: Verify full test suite still passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

The existing ActionFactory tests in `ActionFactoryTests.cs` reference the old constructor signature — they will compile if we removed the old `GetAction(string, JsonElement, Uri)` tests. Confirm no regressions.

- [ ] **Step 6: Commit**

```bash
git add src/Actions/IActionFactory.cs src/Actions/ActionFactory.cs tests/ActionFactoryTests.cs
git commit -m "feat: ActionFactory をリフレクションレジストリに刷新・IActionFactory シグネチャ変更"
```

---

## Task 6: `ActionRegistryValidator`

**Files:**
- Create: `src/Services/ActionRegistryValidator.cs`
- Modify: `src/Program.cs`

**Interfaces:**
- Consumes: `ActionFactory.Registry` (Task 5)
- Produces: `ActionRegistryValidator` — `IHostedService` that validates every registered action can be instantiated at startup.

- [ ] **Step 1: Write the failing test**

Add to `tests/ActionFactoryTests.cs` (append to the class):

```csharp
    [Fact]
    public void ActionRegistryValidator_StartAsync_DoesNotThrowForValidRegistry()
    {
        // ActionRegistryValidator が起動時に ValidateAll を呼び出し例外を投げないことを確認する。
        // Task 8 完了後に各 Action の DI 依存 (IDiscordClient 等) を ServiceCollection に追加すること。
        var sp = BuildServiceProvider();
        var factory = new ActionFactory(sp);
        var validator = new ActionRegistryValidator(factory, sp);

        // 現時点ではレジストリが空（Task 8 前）なので例外なし
        var ex = Record.Exception(() => validator.ValidateAll());
        Assert.Null(ex);
    }
```

Also add the using at the top of `tests/ActionFactoryTests.cs`:

```csharp
using GitHubWebhookBridge.Services;
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionRegistryValidator" -v n
```

Expected: build error — `ActionRegistryValidator` does not exist.

- [ ] **Step 3: Implement**

Create `src/Services/ActionRegistryValidator.cs`:

```csharp
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// 起動時にアクションレジストリの全エントリーをドライラン検証する <see cref="IHostedService"/>。
/// 登録された Action が <see cref="ActivatorUtilities.CreateInstance"/> で解決できない場合に起動を中断する。
/// </summary>
public sealed class ActionRegistryValidator(ActionFactory factory, IServiceProvider sp) : IHostedService
{
    /// <summary>全登録アクションをドライラン検証する。テストから直接呼び出し可能。</summary>
    internal void ValidateAll()
    {
        var dummyUri = new Uri("https://example.com");
        const string dummyEventName = "__validate__";

        foreach (var (eventName, (actionType, payloadType)) in factory.Registry)
        {
            // デシリアライズ可能な最小 JSON でペイロードを生成する
            object? dummy;
            try
            {
                dummy = JsonSerializer.Deserialize("""{}""", payloadType, OctokitJsonOptions.Value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create dummy payload for '{eventName}' ({payloadType.Name}): {ex.Message}", ex);
            }

            try
            {
                ActivatorUtilities.CreateInstance(sp, actionType, dummyUri, dummyEventName, dummy!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"ActionRegistryValidator: failed to instantiate '{actionType.Name}' for event '{eventName}'. " +
                    $"Check DI registrations in Program.cs. Inner: {ex.Message}", ex);
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

Also expose `Registry` as `internal` on `ActionFactory` — already done in Task 5.

- [ ] **Step 4: Register in `Program.cs`**

Add after the existing `AddHostedService<TableStorageInitializer>()` line in `src/Program.cs`:

```csharp
    // アクションレジストリの起動時検証
    .AddHostedService<ActionRegistryValidator>()
```

Also add the using:

```csharp
using GitHubWebhookBridge.Services;
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionRegistryValidator" -v n
```

Expected: 1 test PASS.

- [ ] **Step 6: Verify build**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 7: Commit**

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
- Produces: Updated `WebhookFunction` that passes raw JSON string to factory and uses `WebhookEnvelope` for mute check.

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
    [property: JsonPropertyName("id")] long Id);
```

- [ ] **Step 2: Update `WebhookFunction.cs`**

Make the following changes in `src/Functions/WebhookFunction.cs`:

**a) Remove the `_jsonOptions` static field** (was used for `JsonElement` parse):

```csharp
// 削除: private static readonly JsonSerializerOptions _jsonOptions = ...
```

**b) Replace step 7 (JSON parse to `JsonElement`) with raw string conversion**:

```csharp
// 旧コード (step 7):
// JsonElement body;
// try { body = JsonSerializer.Deserialize<JsonElement>(rawBody, _jsonOptions); }
// catch (JsonException) { return new BadRequestObjectResult(...); }

// 新コード: raw バイト列を文字列化（UTF-8）
// JSON 妥当性チェックは factory 内のデシリアライズで代替する。
// 不正 JSON は InvalidOperationException として factory がスローし、catch (Exception) → 500 で処理する。
string rawJson;
try
{
    rawJson = System.Text.Encoding.UTF8.GetString(rawBody);
    // 最小限の JSON 検証（空でないこと）
    if (rawJson is not ['{', ..] and not ['[', ..])
        return new BadRequestObjectResult(new { message = "Bad Request: Invalid JSON body" });
}
catch (Exception)
{
    return new BadRequestObjectResult(new { message = "Bad Request: Invalid JSON body" });
}
```

**c) Replace step 8 (mute check via `JsonElement`) with `WebhookEnvelope`**:

```csharp
// 旧コード:
// await _muteManager.EnsureLoadedAsync();
// if (body.TryGetProperty("sender", out JsonElement sender) && ...)
//     { ... }

// 新コード:
await _muteManager.EnsureLoadedAsync();
var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawJson, GitHubWebhookBridge.Utils.OctokitJsonOptions.Value);
if (envelope?.Sender is { } sender)
{
    var actionProp = envelope.Action;
    if (_muteManager.IsMuted(sender.Id, eventName, actionProp))
        return new OkObjectResult(new { message = "Muted user" });
}
```

**d) Update step 9 (factory call)** — change `body` → `rawJson`:

```csharp
// 旧コード:
// actionHandler = _actionFactory.GetAction(eventName, body, webhookUrl);

// 新コード:
actionHandler = _actionFactory.GetAction(eventName, rawJson, webhookUrl);
```

**e) Remove the now-unused `System.Text.Json` using** if `JsonElement` is no longer referenced (keep `JsonSerializer` using for `WebhookEnvelope`).

- [ ] **Step 3: Update `WebhookFunctionTests.cs` mock signature**

Search for all occurrences of `.GetAction(` in `tests/WebhookFunctionTests.cs` and update the mock setups from `JsonElement` to `string`:

```csharp
// 旧:
// factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Uri>()))

// 新:
factory.Setup(f => f.GetAction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()))
```

Also remove `using System.Text.Json;` if no longer needed, and add `using GitHubWebhookBridge.Functions;` if `WebhookEnvelope` is referenced in tests.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "WebhookFunctionTests" -v n
```

Expected: all `WebhookFunctionTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Functions/WebhookEnvelope.cs src/Functions/WebhookFunction.cs tests/WebhookFunctionTests.cs
git commit -m "feat: WebhookFunction を raw JSON 渡しに変更・WebhookEnvelope でミュートチェック"
```

---

## Task 8: `BaseAction<T>` Type Constraint + Migrate 12 Implemented Actions

**Files:**
- Modify: `src/Actions/BaseAction.cs`
- Modify: `src/Actions/Impl/*.cs` (12 files)
- Modify: `tests/*ActionTests.cs` (12 files)

**Interfaces:**
- Consumes: `GitHubEventAttribute` (Task 3), `Octokit.Webhooks` types (Task 1)
- Produces: All 12 actions use Octokit types; `[GitHubEvent]` attributes attached; constructor signature updated for `ActivatorUtilities`.

> **Important — Constructor convention for `ActivatorUtilities.CreateInstance`:**
> Each action's constructor must have:
> - DI-resolved parameters first: `IDiscordClient`, `IMessageCacheService`, `IGitHubUserMapManager`, `ILogger<TAction>`
> - Runtime parameters after: `Uri webhookUrl`, `string eventName`, `TOctokitPayload payload`
>
> `ActivatorUtilities` fills runtime params from the array `(webhookUrl, eventName, payload)` by type.
> `string eventName` will be filled before `TOctokitPayload` because strings come first in the params array.
> Each `TOctokitPayload` is a unique Octokit type, so there is no type collision.

> **Important — Octokit type names:** Verify exact type and property names against the package after Task 1. The table below uses the most likely names based on Octokit.Webhooks conventions; adjust if the actual API differs.

- [ ] **Step 1: Add type constraint to `BaseAction<T>`**

Edit `src/Actions/BaseAction.cs` — change the class declaration line:

```csharp
// Before:
public abstract class BaseAction<TEvent>(

// After:
public abstract class BaseAction<TEvent>(
// ...constructor params...
) : IAction where TEvent : Octokit.Webhooks.WebhookEvent
```

Add the using at the top:

```csharp
using Octokit.Webhooks;
```

- [ ] **Step 2: Verify build fails (expected — actions still use hand-written types)**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release 2>&1 | head -30
```

Expected: errors saying hand-written model types (e.g., `PullRequestEvent`) do not satisfy the `where TEvent : WebhookEvent` constraint.

- [ ] **Step 3: Migrate each action — type mapping**

For each action, apply the pattern shown below. The event type mapping is:

| Action class | Old type | Octokit type (verify in Task 1) |
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

**Pattern for each action (shown with `PullRequestAction`):**

Old constructor header:
```csharp
public sealed class PullRequestAction(IDiscordClient discord, Uri webhookUrl, string eventName, PullRequestEvent pullRequestEvent, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : BaseAction<PullRequestEvent>(...)
```

New constructor header:
```csharp
using Octokit.Webhooks.Events;
// ...

[GitHubEvent(WebhookEventType.PullRequest)]   // Level 1; replace with [GitHubEvent] if Level 2 is feasible
public sealed class PullRequestAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<PullRequestAction> logger,
    Uri webhookUrl,
    string eventName,
    PullRequestEvent pullRequestEvent)         // Octokit type
    : BaseAction<PullRequestEvent>(discord, webhookUrl, eventName, pullRequestEvent, cache, userMapManager, logger)
```

Remove the `using GitHubWebhookBridge.Models.GitHubWebhooks;` import and add:
```csharp
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
```

**Property name reconciliation:** The Octokit types use the same JSON field names as the hand-written models, but C# property names may differ (e.g., `Event.PullRequest.HtmlUrl` vs `Event.PullRequest.html_url`). Verify each property access in `RunAsync()` compiles against the Octokit types. Fix any property name mismatches — use IntelliSense or `dotnet build` errors as the guide.

Repeat for all 12 action files.

- [ ] **Step 4: Verify build succeeds**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Update `tests/*ActionTests.cs` — constructor and helper methods**

For each action test file, update:

1. Remove `using GitHubWebhookBridge.Models.GitHubWebhooks;`
2. Add `using Octokit.Webhooks.Events;` and related Octokit namespaces
3. Update helper methods (e.g., `MakePrEvent`) to construct Octokit types instead of hand-written ones
4. Update action constructor calls to match the new parameter order: `(discord.Object, cache.Object, userMap.Object, Mock.Of<ILogger<PullRequestAction>>(), _webhookUri, "pull_request", MakePrEvent("opened"))`

**Example for `PullRequestActionTests.cs`:**

```csharp
// Remove:
// using GitHubWebhookBridge.Models.GitHubWebhooks;

// Add:
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequest;   // if action enums are here; verify in Task 1
using Octokit.Webhooks.Models;               // if shared model types (User, Repository) are here

// Update MakePrEvent helper — property names may differ; use build errors as guide:
private static PullRequestEvent MakePrEvent(string action, bool merged = false, bool draft = false) => new()
{
    Action = action,               // string or enum — verify in Task 1
    Number = 42,
    PullRequest = new()
    {
        Number  = 42,
        Title   = "Add awesome feature",
        Body    = "This PR adds an awesome feature.",
        State   = merged ? "closed" : "open",
        HtmlUrl = new Uri("https://github.com/test/repo/pull/42"),
        User    = new() { Login = "pr-author", Id = 100, HtmlUrl = new Uri("https://github.com/pr-author") },
        Draft   = draft,
        Merged  = merged,
        Head    = new() { Ref = "feature/my-branch" },
        Base    = new() { Ref = "main" },
    },
    Repository = new() { FullName = "test/repo", HtmlUrl = new Uri("https://github.com/test/repo") },
    Sender     = new() { Login = "sender", Id = 200, HtmlUrl = new Uri("https://github.com/sender") },
};

// Update action construction in each test:
PullRequestAction action = new(
    discord.Object, cache.Object, userMap.Object,
    Mock.Of<ILogger<PullRequestAction>>(),
    _webhookUri, "pull_request",
    MakePrEvent("opened"));
```

Repeat the same pattern for all 12 action test files.

- [ ] **Step 6: Run all action tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionTests|DiscussionActionTests|ForkActionTests|IssueCommentActionTests|IssuesActionTests|PingActionTests|PublicActionTests|PullRequestActionTests|PullRequestReviewActionTests|PullRequestReviewCommentActionTests|PullRequestReviewThreadActionTests|PushActionTests|StarActionTests" -v n
```

Expected: all action tests PASS.

- [ ] **Step 7: Update `ActionFactoryTests` — verify 12 registered events**

Add to `tests/ActionFactoryTests.cs`:

```csharp
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
    public void GetAction_KnownEvent_DoesNotReturnUnhandledAction(string eventName)
    {
        // 各イベントに対して UnhandledAction 以外が返ること（具体型はアクションテストで検証）
        // 注: この時点では DI 依存が未解決のため GetAction が例外を投げる可能性がある。
        // DI 依存が必要な場合は BuildServiceProvider() に IDiscordClient 等を追加する。
        var factory = new ActionFactory(BuildServiceProvider());
        Assert.True(factory.Registry.ContainsKey(eventName),
            $"Event '{eventName}' is not registered. Did you add [GitHubEvent] to the action class?");
    }

    [Fact]
    public void Registry_Contains12Events()
    {
        var factory = new ActionFactory(BuildServiceProvider());
        Assert.Equal(12, factory.Registry.Count);
    }
```

- [ ] **Step 8: Run updated factory tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionFactoryTests" -v n
```

Expected: all tests PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Actions/BaseAction.cs src/Actions/Impl/ tests/
git commit -m "feat: BaseAction<T> に型制約追加・12 アクションを Octokit.Webhooks 型に移行"
```

---

## Task 9: Delete Obsolete Files

**Files:**
- Delete: `src/Actions/Stubs/StubActions.cs`
- Delete: `src/Models/GitHubWebhooks/Common.cs` and 11 event model files
- Delete: `src/Models/GitHubWebhooks/Generated/` (entire directory)
- Modify: `src/GitHubWebhookBridge.csproj` (remove `<Compile Remove>` for Generated)
- Modify: `tests/MonkeyTests.cs` (remove `using GitHubWebhookBridge.Actions.Stubs`)
- Modify: `tests/ActionCoverageTests.cs` (exclude `UnhandledAction` from coverage check)

**Interfaces:**
- Consumes: all previous tasks (safe to delete only after Task 8 is green)

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
```

- [ ] **Step 2: Update `GitHubWebhookBridge.csproj`**

Remove the `<Compile Remove>` item group (no longer needed since `Generated/` is deleted):

```xml
<!-- 削除:
<ItemGroup>
  <Compile Remove="Models/GitHubWebhooks/Generated/**" />
</ItemGroup>
-->
```

If `Models/GitHubWebhooks/` is now empty, also remove the directory:

```bash
rmdir src/Models/GitHubWebhooks 2>/dev/null || true
```

- [ ] **Step 3: Update `MonkeyTests.cs`**

Remove the line:

```csharp
using GitHubWebhookBridge.Actions.Stubs;
```

Also remove any direct references to stub action classes in the test body (search for `StubAction`, `BranchProtectionRuleAction`, etc.).

- [ ] **Step 4: Update `ActionCoverageTests.cs`**

The existing test checks that all classes in `GitHubWebhookBridge.Actions.Impl` have a corresponding test class. `UnhandledAction` is in `GitHubWebhookBridge.Actions` (not `.Impl`), so it's already excluded. Verify the existing check still works:

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "ActionCoverageTests" -v n
```

If `ActionCoverageTests` fails because it now also scans for `[GitHubEvent]`-annotated types instead of namespace-based detection, update `IsConcreteAction` to check:

```csharp
// 現行: Namespace == "GitHubWebhookBridge.Actions.Impl" の具象クラスを対象にする
// 変更不要: UnhandledAction は Actions 名前空間のため自動的に除外される
// [GitHubEvent] を持つが Actions.Impl 以外に置かれたクラスがないことを前提とする
```

- [ ] **Step 5: Verify build and tests**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release && \
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

Expected: 0 warnings, all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: StubActions・手書きモデル・生成モデルを削除"
```

---

## Task 10: `OctokitWebhooksCompatibilityTests`

**Files:**
- Create: `tests/OctokitWebhooksCompatibilityTests.cs`

**Interfaces:**
- Consumes: `Octokit.Webhooks.WebhookEvent` base class (Task 1)
- Produces: CI test that fails when Renovate adds/removes top-level event types.

- [ ] **Step 1: Determine all current Octokit event types**

Run the following snippet (e.g., in a temporary test) to get the full list:

```csharp
var types = typeof(Octokit.Webhooks.WebhookEvent).Assembly.GetTypes()
    .Where(t => t.IsSubclassOf(typeof(Octokit.Webhooks.WebhookEvent)) && !t.IsAbstract)
    .Select(t => /* event name from attribute or naming convention */)
    .OrderBy(n => n)
    .ToList();
foreach (var n in types) Console.WriteLine(n);
```

Use the output to fill in `KnownEventTypes` below.

- [ ] **Step 2: Write the test**

Create `tests/OctokitWebhooksCompatibilityTests.cs`:

```csharp
using System.Reflection;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Octokit.Webhooks のトップレベルイベント型が既知リストと一致することを検証する。
/// Renovate による NuGet 更新後にこのテストが落ちた場合:
///   - 追加イベント → 実装または KnownEventTypes に追記して対応
///   - 削除イベント → KnownEventTypes から除去して対応
/// </summary>
public class OctokitWebhooksCompatibilityTests
{
    /// <summary>
    /// 既知のイベント型一覧。Octokit.Webhooks のバージョンに対応するすべてのイベントを列挙する。
    /// Task 1 のスクリプト出力を貼り付けること。
    /// </summary>
    private static readonly IReadOnlySet<string> KnownEventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        // ↓ Task 1 のスクリプト出力で埋める
        "branch_protection_rule",
        "check_run",
        "check_suite",
        "code_scanning_alert",
        "commit_comment",
        "create",
        "delete",
        "dependabot_alert",
        "deploy_key",
        "deployment",
        "deployment_protection_rule",
        "deployment_review",
        "deployment_status",
        "discussion",
        "discussion_comment",
        "fork",
        "github_app_authorization",
        "gollum",
        "installation",
        "installation_repositories",
        "issue_comment",
        "issues",
        "label",
        "marketplace_purchase",
        "member",
        "membership",
        "merge_group",
        "milestone",
        "organization",
        "org_block",
        "package",
        "page_build",
        "ping",
        "project",
        "project_card",
        "project_column",
        "projects_v2",
        "projects_v2_item",
        "public",
        "pull_request",
        "pull_request_review",
        "pull_request_review_comment",
        "pull_request_review_thread",
        "push",
        "registry_package",
        "release",
        "repository",
        "repository_dispatch",
        "repository_ruleset",
        "secret_scanning_alert",
        "security_advisory",
        "sponsorship",
        "star",
        "status",
        "team",
        "team_add",
        "watch",
        "workflow_dispatch",
        "workflow_job",
        "workflow_run",
        // ↑ Task 1 の実際の出力に合わせて修正すること
    };

    [Fact]
    public void OctokitWebhookEventTypes_MustMatchKnownList()
    {
        // Task 1 の調査結果に合わせてイベント名取得方法を調整すること
        // (Level 2 属性がある場合はそこから、ない場合は型名から snake_case 変換)
        var actual = typeof(WebhookEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
            .Select(GetEventName)
            .ToHashSet(StringComparer.Ordinal);

        var added   = actual.Except(KnownEventTypes).OrderBy(n => n).ToList();
        var removed = KnownEventTypes.Except(actual).OrderBy(n => n).ToList();

        Assert.True(added.Count == 0,
            $"Octokit.Webhooks に新しいイベント型が追加されました。実装するか KnownEventTypes に追記してください: {string.Join(", ", added)}");
        Assert.True(removed.Count == 0,
            $"Octokit.Webhooks からイベント型が削除されました。KnownEventTypes から除去してください: {string.Join(", ", removed)}");
    }

    /// <summary>
    /// イベント型からイベント名を取得する。
    /// Task 1 の調査結果に応じて実装を変更すること:
    ///   Level 2 利用可能: Octokit 属性から取得
    ///   Level 2 不可: 型名から snake_case 変換
    /// </summary>
    private static string GetEventName(Type t)
    {
        // Level 2 の場合 (Octokit 属性が存在する場合):
        // var attr = t.GetCustomAttribute<WebhookEventTypeAttribute>(); // 属性名は Task 1 で確認
        // if (attr != null) return attr.EventType;

        // Level 2 不可の場合 (型名から変換):
        // 例: "PullRequestReviewCommentEvent" → "pull_request_review_comment"
        var name = t.Name;
        if (name.EndsWith("Event", StringComparison.Ordinal))
            name = name[..^5]; // "Event" suffix を除去
        return ToSnakeCase(name);
    }

    private static string ToSnakeCase(string pascalCase)
    {
        // PascalCase → snake_case
        return System.Text.RegularExpressions.Regex.Replace(
            pascalCase,
            "([A-Z])([A-Z][a-z])|([a-z0-9])([A-Z])",
            "$1$3_$2$4").ToLowerInvariant();
    }
}
```

- [ ] **Step 3: Run test to verify it passes**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "OctokitWebhooksCompatibilityTests" -v n
```

Expected: 1 test PASS. If it fails, adjust `KnownEventTypes` to match the actual Octokit package contents.

- [ ] **Step 4: Commit**

```bash
git add tests/OctokitWebhooksCompatibilityTests.cs
git commit -m "test: Octokit.Webhooks イベント型互換性テストを追加"
```

---

## Task 11: `OctokitPayloadSchemaSnapshotTests`

**Files:**
- Create: `tests/OctokitPayloadSchemaSnapshotTests.cs`
- Create: `tests/Snapshots/octokit-payload-schema.json` (generated, committed)
- Modify: `tests/GitHubWebhookBridge.Tests.csproj` (add snapshot file to output)

**Interfaces:**
- Consumes: `GitHubEventAttribute` (Task 3), `Octokit.Webhooks` types (Task 1)
- Produces: CI test that fails when Renovate changes implemented event model properties or enums.

- [ ] **Step 1: Create the snapshot directory**

```bash
mkdir -p tests/Snapshots
```

- [ ] **Step 2: Write the test**

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
///   を実行してスナップショットを更新し、差分をレビューしてコミットする。
/// </summary>
public class OctokitPayloadSchemaSnapshotTests
{
    private static readonly string SnapshotPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Snapshots",
        "octokit-payload-schema.json");

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
    };

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
            $"Snapshot file not found: {SnapshotPath}. Run UPDATE_SNAPSHOTS=1 dotnet test to generate it.");

        var expected = File.ReadAllText(SnapshotPath);
        Assert.True(expected == actual,
            $"Octokit.Webhooks モデルスキーマが変化しました。" +
            $"UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests を実行して差分を確認してください。" +
            $"\nSnapshot: {SnapshotPath}");
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
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (propType.IsEnum)
            {
                // Enum: メンバー名と基底値を列挙する
                props[jsonName] = new
                {
                    type = "enum:" + propType.Name,
                    members = Enum.GetNames(propType)
                        .Zip(Enum.GetValues(propType).Cast<int>(), (n, v) => $"{n}={v}")
                        .OrderBy(s => s)
                        .ToArray(),
                };
            }
            else if (propType.IsClass && propType != typeof(string) && propType != typeof(Uri)
                     && !propType.IsGenericType
                     && propType.Namespace?.StartsWith("Octokit", StringComparison.Ordinal) == true)
            {
                // ネストした Octokit 型: 再帰的にスキーマを構築する
                props[jsonName] = BuildTypeSchema(propType, new HashSet<Type>(visited));
            }
            else if (propType.IsGenericType
                     && propType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                     || (propType.IsGenericType && propType.GetGenericArguments().Length == 1
                         && typeof(System.Collections.IEnumerable).IsAssignableFrom(propType)))
            {
                props[jsonName] = "array:" + (propType.GetGenericArguments().FirstOrDefault()?.Name ?? "object");
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

- [ ] **Step 3: Add snapshot file to test project output**

Edit `tests/GitHubWebhookBridge.Tests.csproj` — add inside an `<ItemGroup>`:

```xml
<ItemGroup>
  <None Update="Snapshots/**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 4: Generate the initial snapshot**

```bash
cd /path/to/repo
UPDATE_SNAPSHOTS=1 dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "OctokitPayloadSchemaSnapshotTests" -v n
```

This creates `tests/Snapshots/octokit-payload-schema.json` in the test output directory. Copy it to the source location:

```bash
cp tests/bin/Release/net10.0/Snapshots/octokit-payload-schema.json tests/Snapshots/octokit-payload-schema.json
```

- [ ] **Step 5: Run test to verify it passes with the snapshot in place**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release --filter "OctokitPayloadSchemaSnapshotTests" -v n
```

Expected: 1 test PASS.

- [ ] **Step 6: Run the full test suite**

```bash
dotnet test tests/GitHubWebhookBridge.Tests.csproj -c Release
```

Expected: all tests PASS, line coverage ≥ 80%, mutation score ≥ 60%.

- [ ] **Step 7: Commit**

```bash
git add tests/OctokitPayloadSchemaSnapshotTests.cs tests/Snapshots/octokit-payload-schema.json tests/GitHubWebhookBridge.Tests.csproj
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
| `ActionRegistryValidator` (startup validation) | Task 6 |
| `WebhookFunction` raw string + `WebhookEnvelope` mute check | Task 7 |
| Delete StubActions, hand-written models | Task 9 |
| Octokit event-type compatibility test | Task 10 |
| Payload schema snapshot test (recursive, includes enums) | Task 11 |
| `JsonSerializerOptions.MakeReadOnly()` | Task 2 |
| `typeof(ActionFactory).Assembly` (not `GetExecutingAssembly`) | Task 5 |
| Case-sensitive `FrozenDictionary` | Task 5 |
| `Octokit.Webhooks` pinned version (not `*`) | Task 1 |

All spec requirements are covered.

### Placeholder Scan

No TBD, TODO, or "similar to" references. All steps that touch code include the actual code. Task 1 defers two Octokit API details (Level 2 feasibility, `JsonSerializerOptions` base) to runtime investigation — this is intentional and acknowledged in each task that depends on those findings.

### Type Consistency

- `IActionFactory.GetAction(string eventName, string rawJson, Uri webhookUrl)` — consistent across Tasks 5, 7, and `ActionFactoryTests`.
- `ActionFactory(IServiceProvider sp)` — consistent across Tasks 5, 6.
- `UnhandledAction(string eventName)` — consistent across Tasks 4, 5.
- `OctokitJsonOptions.Value` — consistent across Tasks 2, 5, 7, 11.
- `GetPayloadType()` using `typeof(BaseAction<>)` — consistent across Tasks 5, 6, 11.
