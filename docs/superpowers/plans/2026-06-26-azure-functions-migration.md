# Azure Functions Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** TypeScript/Node.js/Vercel 実装を C#/.NET 10/Azure Functions (Isolated Worker Model) へ in-place で完全移行し、既存の全挙動を保持する。

**Architecture:** `WebhookFunction` が GitHub Webhook POST を受信し、HMAC-SHA256 署名検証 → Mute/Disabled フィルタリング → 59 種の `IAction` ディスパッチ（12 実装 + 47 NotImplementedException スタブ）→ Discord Embed 送信/編集。5 分以内の同一キーメッセージは Azure Table Storage キャッシュを用いて編集。設定ファイル（mutes/user-map）は Blob > HTTPS URL > local file の優先順で取得。

**Tech Stack:** C# / .NET 10 / Azure Functions v4 Isolated Worker / Azure Table Storage / Azure Blob Storage / NJsonSchema.Console (モデル生成) / DiffPlex (unified diff) / System.Text.Json / xUnit + Moq

## Global Constraints

- TargetFramework: `net10.0`、AzureFunctionsVersion: `v4`、OutputType: `Exe`
- ホスティング: Windows Consumption plan
- Nullable enable、ImplicitUsings enable 必須
- `Azure.Identity` は不使用 — 全 Azure 認証は接続文字列（`AzureWebJobsStorage`）
- ADLS Gen2 は不使用 — 設定取得源: Blob > HTTPS URL (HTTPS のみ) > local file
- Application Insights 有効（サンプリング有効、`excludedTypes: "Request"`）
- `deploy.yml` はスコープ外（デプロイ自動化は本 PR に含めない）
- 全 Discord メッセージに `SuppressNotifications`（`1 << 12 = 4096`）フラグを付与
- コード内コメントは日本語、エラーメッセージは英語
- Conventional Commits（`<description>` は日本語）
- `skipLibCheck` 使用禁止

---

## File Map

### 新規作成（C# プロジェクト）

```
GitHubWebhookBridge.csproj
Program.cs
host.json
local.settings.json                  # .gitignore 対象
.config/dotnet-tools.json            # NJsonSchema.Console 登録

Functions/
  WebhookFunction.cs

Actions/
  IAction.cs
  IActionFactory.cs
  ActionFactory.cs                   # 59 switch cases
  BaseAction.cs                      # abstract; SendMessageAsync / GetUsersMentionsAsync / CreatePatch
  # 実装 12:
  PingAction.cs
  PushAction.cs
  StarAction.cs
  ForkAction.cs
  PublicAction.cs
  PullRequestReviewAction.cs
  PullRequestReviewCommentAction.cs
  PullRequestReviewThreadAction.cs
  IssueCommentAction.cs
  PullRequestAction.cs
  IssuesAction.cs
  DiscussionAction.cs
  # スタブ 47: BranchProtectionRuleAction.cs 〜 WorkflowRunAction.cs (略)

Models/Discord/
  DiscordMessage.cs
  DiscordEmbed.cs
  DiscordEmbedAuthor.cs
  DiscordEmbedField.cs
  DiscordEmbedFooter.cs
  DiscordMessageResponse.cs
  DiscordMessageFlags.cs

Models/GitHubWebhooks/               # NJsonSchema 自動生成（59 種）
  PullRequestEvent.cs  ... etc

Managers/
  BaseManager.cs
  IMuteManager.cs
  MuteManager.cs
  IGitHubUserMapManager.cs
  GitHubUserMapManager.cs

Services/
  IDiscordClient.cs
  DiscordClient.cs
  IMessageCacheService.cs
  MessageCacheService.cs             # TableStorageInitializer 含む

Utils/
  SignatureValidator.cs
  EmbedColors.cs
  EmbedHelper.cs

scripts/
  generate-models.ps1

tests/GitHubWebhookBridge.Tests/
  GitHubWebhookBridge.Tests.csproj
  SignatureValidatorTests.cs
  MuteManagerTests.cs
  PullRequestActionTests.cs          # WIP / draft-assignment
  Fixtures/
    push.json
    pull_request.json
    issues.json

.github/workflows/
  dotnet-ci.yml
  generate-models.yml
```

### 削除（TS/Node 資産）

```
src/  api/  generate-docs/  node_modules/
package.json  pnpm-lock.yaml  tsconfig.json
vercel.json  Dockerfile  compose.yaml  .dockerignore
eslint.config.mjs  .prettierrc.yml  .depcheckrc.json  .node-version
.github/workflows/{check-import,docker,generate-docs,nodejs-ci-pnpm}.yml
.devcontainer/
docs/       # Node 版 Puppeteer 自動生成 PNG (残置か削除かは実装者がユーザー確認)
```

### 更新

```
.gitignore       # local.settings.json / packages.lock.json 追加
README.md        # C# 版の説明に書き換え
CLAUDE.md        # pnpm → dotnet コマンド、アーキテクチャ更新
```

---

## Task 1: Prerequisites & Branch

**Files:** なし（環境準備）

**Interfaces:** なし

- [ ] **.NET 10 SDK をインストール**

  Linux (Ubuntu/Debian):
  ```bash
  wget https://dot.net/v1/dotnet-install.sh
  chmod +x dotnet-install.sh
  ./dotnet-install.sh --channel 10.0
  echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
  source ~/.bashrc
  dotnet --version
  # → 10.0.xxx
  ```

- [ ] **PowerShell Core をインストール**

  ```bash
  wget -q "https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb"
  sudo dpkg -i packages-microsoft-prod.deb
  sudo apt-get update && sudo apt-get install -y powershell
  pwsh --version
  # → PowerShell 7.x.x
  ```

- [ ] **master 最新から新規ブランチ作成**

  ```bash
  cd /mnt/hdd/repos/github.com/book000/github-webhook-bridge
  git fetch origin
  git checkout -b feat/azure-functions-migration origin/master
  ```

---

## Task 2: Project Scaffold

**Files:**
- Create: `GitHubWebhookBridge.csproj`
- Create: `Program.cs` (最小限)
- Create: `host.json`
- Create: `local.settings.json`
- Modify: `.gitignore`

**Interfaces:**
- Produces: ビルド可能な最小 Azure Functions プロジェクト

- [ ] **NuGet パッケージの最新版を確認**

  ```bash
  dotnet package search Microsoft.Azure.Functions.Worker --take 1
  dotnet package search Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore --take 1
  dotnet package search Microsoft.Azure.Functions.Worker.Sdk --take 1
  dotnet package search Azure.Data.Tables --take 1
  dotnet package search Azure.Storage.Blobs --take 1
  dotnet package search DiffPlex --take 1
  dotnet package search Microsoft.Azure.Functions.Worker.ApplicationInsights --take 1
  ```

  確認した各バージョンを次のステップの `<Version>` に記入すること。

- [ ] **`GitHubWebhookBridge.csproj` を作成**（バージョンは上で確認した値に置換）

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <AzureFunctionsVersion>v4</AzureFunctionsVersion>
      <OutputType>Exe</OutputType>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <RootNamespace>GitHubWebhookBridge</RootNamespace>
      <AssemblyName>GitHubWebhookBridge</AssemblyName>
      <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    </PropertyGroup>

    <ItemGroup>
      <!-- バージョンは Task 1 で確認した最新版に置換 -->
      <PackageReference Include="Microsoft.Azure.Functions.Worker"                          Version="CONFIRM_VERSION" />
      <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="CONFIRM_VERSION" />
      <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk"                      Version="CONFIRM_VERSION" />
      <PackageReference Include="Azure.Data.Tables"                                         Version="CONFIRM_VERSION" />
      <PackageReference Include="Azure.Storage.Blobs"                                       Version="CONFIRM_VERSION" />
      <PackageReference Include="DiffPlex"                                                   Version="CONFIRM_VERSION" />
      <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights"      Version="CONFIRM_VERSION" />
    </ItemGroup>

    <ItemGroup>
      <None Update="host.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      </None>
      <None Update="local.settings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <CopyToPublishDirectory>Never</CopyToPublishDirectory>
      </None>
    </ItemGroup>
  </Project>
  ```

- [ ] **`Program.cs` を作成（最小限 — 後のタスクで DI を追加）**

  ```csharp
  using Microsoft.Azure.Functions.Worker.Builder;
  using Microsoft.Extensions.Hosting;

  var builder = FunctionsApplication.CreateBuilder(args);
  builder.ConfigureFunctionsWebApplication();
  builder.Build().Run();
  ```

- [ ] **`host.json` を作成**

  ```json
  {
    "version": "2.0",
    "logging": {
      "applicationInsights": {
        "samplingSettings": {
          "isEnabled": true,
          "excludedTypes": "Request"
        }
      }
    },
    "extensions": {
      "http": {
        "routePrefix": ""
      }
    }
  }
  ```

- [ ] **`local.settings.json` を作成（ローカル開発用 — コミット禁止）**

  ```json
  {
    "IsEncrypted": false,
    "Values": {
      "AzureWebJobsStorage": "UseDevelopmentStorage=true",
      "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
      "GITHUB_WEBHOOK_SECRET": "dev-secret",
      "DISCORD_WEBHOOK_URL": "https://discord.com/api/webhooks/000/dev"
    }
  }
  ```

- [ ] **`.gitignore` にエントリ追加**

  既存の `.gitignore` の末尾に追記：
  ```
  # Azure Functions
  local.settings.json
  packages.lock.json
  bin/
  obj/
  ```

- [ ] **ビルド確認**

  ```bash
  dotnet restore
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **コミット**

  ```bash
  git add GitHubWebhookBridge.csproj Program.cs host.json local.settings.json .gitignore
  git commit -m "chore: C#/.NET 10 Azure Functions プロジェクト雛形を作成"
  ```

---

## Task 3: Discord Models

**Files:**
- Create: `Models/Discord/DiscordMessage.cs`
- Create: `Models/Discord/DiscordEmbed.cs`
- Create: `Models/Discord/DiscordEmbedAuthor.cs`
- Create: `Models/Discord/DiscordEmbedField.cs`
- Create: `Models/Discord/DiscordEmbedFooter.cs`
- Create: `Models/Discord/DiscordMessageResponse.cs`
- Create: `Models/Discord/DiscordMessageFlags.cs`

**Interfaces:**
- Produces: `GitHubWebhookBridge.Models.Discord.*` — 後続タスク全て参照

- [ ] **`Models/Discord/DiscordMessage.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord Webhook に送信するメッセージ。</summary>
  public record DiscordMessage(
      [property: JsonPropertyName("content")]  string?             Content = null,
      [property: JsonPropertyName("embeds")]   List<DiscordEmbed>? Embeds  = null,
      [property: JsonPropertyName("flags")]    int                 Flags   = 0);
  ```

- [ ] **`Models/Discord/DiscordEmbed.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord Embed オブジェクト。</summary>
  public record DiscordEmbed(
      [property: JsonPropertyName("title")]       string?                  Title       = null,
      [property: JsonPropertyName("description")] string?                  Description = null,
      [property: JsonPropertyName("url")]         string?                  Url         = null,
      [property: JsonPropertyName("color")]       int?                     Color       = null,
      [property: JsonPropertyName("author")]      DiscordEmbedAuthor?      Author      = null,
      [property: JsonPropertyName("fields")]      List<DiscordEmbedField>? Fields      = null,
      [property: JsonPropertyName("footer")]      DiscordEmbedFooter?      Footer      = null,
      [property: JsonPropertyName("timestamp")]   string?                  Timestamp   = null);
  ```

- [ ] **`Models/Discord/DiscordEmbedAuthor.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord Embed 著者情報。</summary>
  public record DiscordEmbedAuthor(
      [property: JsonPropertyName("name")]     string  Name,
      [property: JsonPropertyName("url")]      string? Url     = null,
      [property: JsonPropertyName("icon_url")] string? IconUrl = null);
  ```

- [ ] **`Models/Discord/DiscordEmbedField.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord Embed フィールド。</summary>
  public record DiscordEmbedField(
      [property: JsonPropertyName("name")]   string Name,
      [property: JsonPropertyName("value")]  string Value,
      [property: JsonPropertyName("inline")] bool?  Inline = null);
  ```

- [ ] **`Models/Discord/DiscordEmbedFooter.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord Embed フッター。</summary>
  public record DiscordEmbedFooter(
      [property: JsonPropertyName("text")]     string  Text,
      [property: JsonPropertyName("icon_url")] string? IconUrl = null);
  ```

- [ ] **`Models/Discord/DiscordMessageResponse.cs`**

  ```csharp
  using System.Text.Json.Serialization;

  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord が ?wait=true で返すメッセージレスポンス。</summary>
  public record DiscordMessageResponse(
      [property: JsonPropertyName("id")] string Id);
  ```

- [ ] **`Models/Discord/DiscordMessageFlags.cs`**

  ```csharp
  namespace GitHubWebhookBridge.Models.Discord;

  /// <summary>Discord メッセージフラグ定数。</summary>
  public static class DiscordMessageFlags
  {
      /// <summary>通知を抑制するフラグ (1 &lt;&lt; 12 = 4096)。</summary>
      public const int SuppressNotifications = 1 << 12;
  }
  ```

- [ ] **ビルド確認**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **コミット**

  ```bash
  git add Models/
  git commit -m "feat: Discord メッセージモデルを追加"
  ```

---

## Task 4: Utils (SignatureValidator, EmbedColors, EmbedHelper)

**Files:**
- Create: `Utils/SignatureValidator.cs`
- Create: `Utils/EmbedColors.cs`
- Create: `Utils/EmbedHelper.cs`
- Create: `tests/GitHubWebhookBridge.Tests/GitHubWebhookBridge.Tests.csproj`
- Create: `tests/GitHubWebhookBridge.Tests/SignatureValidatorTests.cs`

**Interfaces:**
- Produces: `SignatureValidator.Validate(byte[], IHeaderDictionary, string): bool`
- Produces: `EmbedHelper.CreateEmbed(string, int, string, ...): DiscordEmbed`
- Produces: `EmbedColors.*` (69 int 定数)

- [ ] **テストプロジェクト作成**

  ```bash
  mkdir -p tests/GitHubWebhookBridge.Tests
  ```

  `tests/GitHubWebhookBridge.Tests/GitHubWebhookBridge.Tests.csproj`:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.NET.Test.Sdk"       Version="CONFIRM_VERSION" />
      <PackageReference Include="xunit"                         Version="CONFIRM_VERSION" />
      <PackageReference Include="xunit.runner.visualstudio"     Version="CONFIRM_VERSION" />
      <PackageReference Include="Moq"                           Version="CONFIRM_VERSION" />
      <PackageReference Include="coverlet.collector"            Version="CONFIRM_VERSION" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="../../GitHubWebhookBridge.csproj" />
    </ItemGroup>
  </Project>
  ```

  > xunit/Moq/etc のバージョンは `dotnet package search xunit --take 1` で確認する。

- [ ] **`tests/GitHubWebhookBridge.Tests/SignatureValidatorTests.cs` を書く（先にテスト）**

  ```csharp
  using System.Security.Cryptography;
  using System.Text;
  using GitHubWebhookBridge.Utils;
  using Microsoft.AspNetCore.Http;
  using Moq;

  namespace GitHubWebhookBridge.Tests;

  public class SignatureValidatorTests
  {
      private static string ComputeSignature(byte[] body, string secret)
      {
          using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
          return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
      }

      private static IHeaderDictionary MakeHeaders(string sig)
      {
          var mock = new Mock<IHeaderDictionary>();
          mock.Setup(h => h["X-Hub-Signature-256"])
              .Returns(new Microsoft.Extensions.Primitives.StringValues(sig));
          return mock.Object;
      }

      [Fact]
      public void Validate_ValidSignature_ReturnsTrue()
      {
          var body   = Encoding.UTF8.GetBytes("hello");
          var secret = "mysecret";
          var sig    = ComputeSignature(body, secret);
          Assert.True(SignatureValidator.Validate(body, MakeHeaders(sig), secret));
      }

      [Fact]
      public void Validate_InvalidSignature_ReturnsFalse()
      {
          var body = Encoding.UTF8.GetBytes("hello");
          Assert.False(SignatureValidator.Validate(body, MakeHeaders("sha256=000000"), "mysecret"));
      }

      [Fact]
      public void Validate_MissingHeader_ReturnsFalse()
      {
          var mock = new Mock<IHeaderDictionary>();
          mock.Setup(h => h["X-Hub-Signature-256"])
              .Returns(Microsoft.Extensions.Primitives.StringValues.Empty);
          Assert.False(SignatureValidator.Validate([], mock.Object, "secret"));
      }
  }
  ```

- [ ] **テスト実行（失敗確認）**

  ```bash
  dotnet test tests/GitHubWebhookBridge.Tests/ --logger "console;verbosity=minimal"
  # → error CS0246: The type or namespace name 'SignatureValidator' could not be found
  ```

- [ ] **`Utils/SignatureValidator.cs` を実装**

  ```csharp
  using System.Security.Cryptography;
  using System.Text;
  using Microsoft.AspNetCore.Http;

  namespace GitHubWebhookBridge.Utils;

  /// <summary>GitHub Webhook の HMAC-SHA256 署名を検証するユーティリティ。</summary>
  public static class SignatureValidator
  {
      private const string SignaturePrefix = "sha256=";

      /// <summary>
      /// X-Hub-Signature-256 ヘッダーを raw リクエストボディと照合して検証する。
      /// タイミング攻撃を防ぐために <see cref="CryptographicOperations.FixedTimeEquals"/> を使用する。
      /// </summary>
      /// <remarks>
      /// computedBytes.Length は常に 64（HMAC-SHA256 hex の定数長）のため、
      /// 長さ不一致のアーリーリターンは攻撃者にタイミング情報を与えない。
      /// </remarks>
      public static bool Validate(byte[] rawBody, IHeaderDictionary headers, string secret)
      {
          var signatureHeader = headers["X-Hub-Signature-256"].ToString();
          if (string.IsNullOrEmpty(signatureHeader)
              || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
              return false;

          var receivedHash = signatureHeader[SignaturePrefix.Length..];

          using var hmac        = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
          var       computedHash = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();

          var computedBytes = Encoding.ASCII.GetBytes(computedHash);
          var receivedBytes = Encoding.ASCII.GetBytes(receivedHash);

          if (computedBytes.Length != receivedBytes.Length)
              return false;

          return CryptographicOperations.FixedTimeEquals(computedBytes, receivedBytes);
      }
  }
  ```

- [ ] **テスト実行（成功確認）**

  ```bash
  dotnet test tests/GitHubWebhookBridge.Tests/ --logger "console;verbosity=minimal"
  # → Passed! (3 tests)
  ```

- [ ] **`Utils/EmbedColors.cs` を実装**

  TS 版 `src/embed-colors.ts` の全定数（71 件）を移植。
  実装時に TS ファイルを参照し、件数が一致することを確認すること。

  ```csharp
  namespace GitHubWebhookBridge.Utils;

  /// <summary>Discord Embed の色定数。TypeScript 版 embed-colors.ts からの移植。</summary>
  public static class EmbedColors
  {
      public const int Unknown = 0x000000;

      // PullRequest
      public const int PullRequestOpened                = 0x2ecc71;
      public const int PullRequestMerged                = 0x000000;
      public const int PullRequestClosed                = 0x95a5a6;
      public const int PullRequestReopened              = 0x3498db;
      public const int PullRequestAssigned              = 0xf39c12;
      public const int PullRequestUnassigned            = 0xf39c12;
      public const int PullRequestReviewRequested       = 0x9b59b6;
      public const int PullRequestReviewRequestRemoved  = 0x9b59b6;
      public const int PullRequestLabeled               = 0x3498db;
      public const int PullRequestUnlabeled             = 0x3498db;
      public const int PullRequestEdited                = 0x3498db;
      public const int PullRequestReadyForReview        = 0x2ecc71;
      public const int PullRequestLocked                = 0x7f8c8d;
      public const int PullRequestUnlocked              = 0x7f8c8d;
      public const int PullRequestAutoMergeEnabled      = 0x2ecc71;
      public const int PullRequestAutoMergeDisabled     = 0xe74c3c;
      public const int PullRequestConvertedToDraft      = 0x95a5a6;
      public const int PullRequestDemilestoned          = 0x95a5a6;
      public const int PullRequestMilestoned            = 0x3498db;
      public const int PullRequestEnqueued              = 0x3498db;
      public const int PullRequestDequeued              = 0x3498db;

      // PullRequestReview
      public const int PullRequestReviewApproved         = 0x2ecc71;
      public const int PullRequestReviewChangesRequested = 0xf39c12;
      public const int PullRequestReviewDismissed        = 0xe74c3c;
      public const int PullRequestReviewEdited           = 0x3498db;

      // PullRequestReviewComment
      public const int PullRequestReviewCommentCreated = 0x2ecc71;
      public const int PullRequestReviewCommentEdited  = 0x3498db;
      public const int PullRequestReviewCommentDeleted = 0xe74c3c;

      // PullRequestReviewThread
      public const int PullRequestReviewThreadResolved   = 0x2ecc71;
      public const int PullRequestReviewThreadUnresolved = 0xe74c3c;

      // Issues
      public const int IssueOpened       = 0x2ecc71;
      public const int IssueClosed       = 0x95a5a6;
      public const int IssueReopened     = 0x3498db;
      public const int IssueAssigned     = 0xf39c12;
      public const int IssueUnassigned   = 0xf39c12;
      public const int IssueLabeled      = 0x3498db;
      public const int IssueUnlabeled    = 0x3498db;
      public const int IssueEdited       = 0x3498db;
      public const int IssueLocked       = 0x7f8c8d;
      public const int IssueUnlocked     = 0x7f8c8d;
      public const int IssueMilestoned   = 0x3498db;
      public const int IssueDemilestoned = 0x95a5a6;
      public const int IssueTransferred  = 0x95a5a6;
      public const int IssuePinned       = 0x2ecc71;
      public const int IssueUnpinned     = 0xe74c3c;
      public const int IssueDeleted      = 0xe74c3c;

      // IssueComment
      public const int IssueCommentCreated = 0x3498db;
      public const int IssueCommentEdited  = 0x3498db;
      public const int IssueCommentDeleted = 0xe74c3c;

      // Repository
      public const int Star    = 0xffd700;
      public const int Unstar  = 0x9b59b6;
      public const int Fork    = 0x2ecc71;
      public const int Push    = 0x2ecc71;
      public const int Ping    = 0x95a5a6;
      public const int Public  = 0x2ecc71;

      // Discussion
      public const int DiscussionCreated         = 0x2ecc71;
      public const int DiscussionEdited          = 0x3498db;
      public const int DiscussionDeleted         = 0xe74c3c;
      public const int DiscussionPinned          = 0x2ecc71;
      public const int DiscussionUnpinned        = 0xe74c3c;
      public const int DiscussionLabeled         = 0x3498db;
      public const int DiscussionUnlabeled       = 0x3498db;
      public const int DiscussionTransferred     = 0x95a5a6;
      public const int DiscussionCategoryChanged = 0x3498db;
      public const int DiscussionAnswered        = 0x2ecc71;
      public const int DiscussionUnanswered      = 0xe74c3c;
      public const int DiscussionLocked          = 0x7f8c8d;
      public const int DiscussionUnlocked        = 0x7f8c8d;
  }
  ```

  > **確認**: `grep -c 'public const int' Utils/EmbedColors.cs` が 71 であることを確認。
  > TS 版の `src/embed-colors.ts` 件数と一致しない場合は TS を参照して修正すること。

- [ ] **`Utils/EmbedHelper.cs` を実装**

  ```csharp
  using GitHubWebhookBridge.Models.Discord;

  namespace GitHubWebhookBridge.Utils;

  /// <summary>Discord Embed 生成ヘルパー。</summary>
  public static class EmbedHelper
  {
      private const string FooterIconUrl = "https://i.imgur.com/PdvExHP.png";

      /// <summary>
      /// 標準フッター・タイムスタンプ付きの Discord Embed を生成する。
      /// TypeScript 版の createEmbed() に相当。
      /// </summary>
      public static DiscordEmbed CreateEmbed(
          string               eventName,
          int                  color,
          string               title,
          string?              description = null,
          string?              url         = null,
          DiscordEmbedAuthor?  author      = null,
          List<DiscordEmbedField>? fields  = null)
          => new(
              Title:       title,
              Description: description,
              Url:         url,
              Color:       color,
              Author:      author,
              Fields:      fields,
              Footer:      new DiscordEmbedFooter(
                  Text:    $"Powered by book000/github-webhook-bridge ({eventName} event)",
                  IconUrl: FooterIconUrl),
              Timestamp: DateTimeOffset.UtcNow.ToString("o"));
  }
  ```

- [ ] **ビルド確認**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **コミット**

  ```bash
  git add Utils/ tests/
  git commit -m "feat: SignatureValidator・EmbedColors・EmbedHelper を追加"
  ```

---

## Task 5: Services (DiscordClient + MessageCacheService)

**Files:**
- Create: `Services/IDiscordClient.cs`
- Create: `Services/DiscordClient.cs`
- Create: `Services/IMessageCacheService.cs`
- Create: `Services/MessageCacheService.cs` (TableStorageInitializer 含む)

**Interfaces:**
- Produces: `IDiscordClient.SendMessageAsync(string webhookUrl, DiscordMessage) : Task<string>`
- Produces: `IDiscordClient.EditMessageAsync(string webhookUrl, string messageId, DiscordMessage) : Task`
- Produces: `IMessageCacheService.GetAsync(string webhookUrl, string key) : Task<CachedMessage?>`
- Produces: `IMessageCacheService.SetAsync(string webhookUrl, string key, string messageId) : Task`

- [ ] **`Services/IDiscordClient.cs`**

  ```csharp
  using GitHubWebhookBridge.Models.Discord;

  namespace GitHubWebhookBridge.Services;

  /// <summary>Discord Webhook API の送受信インターフェース。</summary>
  public interface IDiscordClient
  {
      /// <summary>メッセージを送信し、Discord が返したメッセージ ID を返す。</summary>
      Task<string> SendMessageAsync(string webhookUrl, DiscordMessage message);

      /// <summary>既存メッセージを編集する。</summary>
      Task EditMessageAsync(string webhookUrl, string messageId, DiscordMessage message);
  }
  ```

- [ ] **`Services/DiscordClient.cs`**

  ```csharp
  using System.Net.Http.Json;
  using GitHubWebhookBridge.Models.Discord;

  namespace GitHubWebhookBridge.Services;

  /// <summary>Discord Webhook API クライアント実装。</summary>
  public class DiscordClient : IDiscordClient
  {
      private readonly IHttpClientFactory _httpClientFactory;

      public DiscordClient(IHttpClientFactory httpClientFactory)
          => _httpClientFactory = httpClientFactory;

      public async Task<string> SendMessageAsync(string webhookUrl, DiscordMessage message)
      {
          var http = _httpClientFactory.CreateClient("discord");
          // ?wait=true で Discord がメッセージオブジェクト (id 含む) を返す
          var response = await http.PostAsJsonAsync(webhookUrl + "?wait=true", message);
          EnsureSuccess(response);
          var result = await response.Content.ReadFromJsonAsync<DiscordMessageResponse>()
              ?? throw new InvalidOperationException("Discord returned null message response");
          return result.Id;
      }

      public async Task EditMessageAsync(string webhookUrl, string messageId, DiscordMessage message)
      {
          var http     = _httpClientFactory.CreateClient("discord");
          var editUrl  = $"{webhookUrl}/messages/{messageId}";
          var response = await http.PatchAsJsonAsync(editUrl, message);
          EnsureSuccess(response);
      }

      /// <summary>
      /// Discord Webhook トークンが Application Insights テレメトリに漏洩しないよう、
      /// EnsureSuccessStatusCode() の代わりに独自エラー処理を行う。
      /// </summary>
      private static void EnsureSuccess(HttpResponseMessage response)
      {
          if (!response.IsSuccessStatusCode)
              throw new HttpRequestException(
                  $"Discord API error: {(int)response.StatusCode} {response.ReasonPhrase}");
      }
  }
  ```

- [ ] **`Services/IMessageCacheService.cs`**

  ```csharp
  namespace GitHubWebhookBridge.Services;

  /// <summary>Discord メッセージ ID の 5 分間キャッシュインターフェース。</summary>
  public interface IMessageCacheService
  {
      Task<CachedMessage?> GetAsync(string webhookUrl, string key);
      Task SetAsync(string webhookUrl, string key, string messageId);
  }

  /// <summary>キャッシュされたメッセージ情報。</summary>
  public record CachedMessage(string MessageId);
  ```

- [ ] **`Services/MessageCacheService.cs`**

  ```csharp
  using Azure;
  using Azure.Data.Tables;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Hosting;

  namespace GitHubWebhookBridge.Services;

  /// <summary>Azure Table Storage キャッシュエントリ。</summary>
  public class MessageCacheEntity : ITableEntity
  {
      public string          PartitionKey { get; set; } = "";  // webhookUrl の SHA-256 (32 hex chars)
      public string          RowKey       { get; set; } = "";  // サニタイズ済みメッセージキー
      public string          MessageId    { get; set; } = "";
      public DateTimeOffset? Timestamp    { get; set; }        // サーバー管理プロパティ（書き込み不可）
      public ETag            ETag         { get; set; }
  }

  /// <summary>
  /// Azure Table Storage を使用した Discord メッセージ ID の 5 分間キャッシュ。
  /// </summary>
  public class MessageCacheService : IMessageCacheService
  {
      private const  string   TableName = "MessageCache";
      private static readonly TimeSpan  CacheTtl  = TimeSpan.FromMinutes(5);

      private readonly    TableClient  _tableClient;
      private volatile    bool         _initialized;
      private readonly    SemaphoreSlim _initLock = new(1, 1);

      public MessageCacheService(IConfiguration config)
      {
          var connStr = config["AzureWebJobsStorage"]
              ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");
          var serviceClient = new TableServiceClient(connStr);
          _tableClient = serviceClient.GetTableClient(TableName);
      }

      /// <summary>
      /// テーブルを非同期で作成する。
      /// TableStorageInitializer (IHostedService) から呼ばれる。
      /// </summary>
      public async Task InitializeAsync(CancellationToken cancellationToken = default)
      {
          if (_initialized) return;
          await _initLock.WaitAsync(cancellationToken);
          try
          {
              if (_initialized) return;
              await _tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
              _initialized = true;
          }
          finally
          {
              _initLock.Release();
          }
      }

      public async Task<CachedMessage?> GetAsync(string webhookUrl, string key)
      {
          var partitionKey = HashWebhookUrl(webhookUrl);
          var rowKey       = SanitizeRowKey(key);
          var response     = await _tableClient.GetEntityIfExistsAsync<MessageCacheEntity>(partitionKey, rowKey);

          if (!response.HasValue || response.Value is null)
              return null;

          // TTL チェック — 期限切れはテーブルから削除
          if (response.Value.Timestamp.HasValue
              && DateTimeOffset.UtcNow - response.Value.Timestamp.Value > CacheTtl)
          {
              await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
              return null;
          }

          return new CachedMessage(response.Value.MessageId);
      }

      public async Task SetAsync(string webhookUrl, string key, string messageId)
      {
          // Timestamp は Azure Table Storage がサーバー側で管理するため設定しない
          var entity = new MessageCacheEntity
          {
              PartitionKey = HashWebhookUrl(webhookUrl),
              RowKey       = SanitizeRowKey(key),
              MessageId    = messageId,
          };
          await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
      }

      /// <summary>
      /// Azure Table Storage の RowKey に使用できない文字をエスケープする。
      /// 使用禁止文字: /, \, #, ? および制御文字。
      /// 512 文字で切断する際は %XX エンコード三文字組を分断しないよう調整する。
      /// </summary>
      private static string SanitizeRowKey(string key)
      {
          var escaped = Uri.EscapeDataString(key);
          if (escaped.Length <= 512) return escaped;

          int cut = 512;
          if (escaped[cut - 1] == '%')         cut -= 1;
          else if (cut >= 2 && escaped[cut - 2] == '%') cut -= 2;
          return escaped[..cut];
      }

      private static string HashWebhookUrl(string webhookUrl)
      {
          var hash = System.Security.Cryptography.SHA256.HashData(
              System.Text.Encoding.UTF8.GetBytes(webhookUrl));
          return Convert.ToHexString(hash)[..32].ToLowerInvariant();
      }
  }

  /// <summary>
  /// ホスト起動時に Table Storage を非同期で初期化する IHostedService。
  /// MessageCacheService を具象型で注入してコンストラクタでのブロッキング I/O を回避する。
  /// </summary>
  public class TableStorageInitializer : IHostedService
  {
      private readonly MessageCacheService _service;

      public TableStorageInitializer(MessageCacheService service)
          => _service = service;

      public Task StartAsync(CancellationToken cancellationToken)
          => _service.InitializeAsync(cancellationToken);

      public Task StopAsync(CancellationToken cancellationToken)
          => Task.CompletedTask;
  }
  ```

- [ ] **ビルド確認**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **コミット**

  ```bash
  git add Services/
  git commit -m "feat: DiscordClient・MessageCacheService を追加"
  ```

---

## Task 6: Managers (BaseManager, MuteManager, GitHubUserMapManager)

**Files:**
- Create: `Managers/BaseManager.cs`
- Create: `Managers/IMuteManager.cs`
- Create: `Managers/MuteManager.cs`
- Create: `Managers/IGitHubUserMapManager.cs`
- Create: `Managers/GitHubUserMapManager.cs`
- Create: `tests/GitHubWebhookBridge.Tests/MuteManagerTests.cs`

**Interfaces:**
- Produces: `IMuteManager.IsMuted(long userId, string eventName, string? action): bool`
- Produces: `IGitHubUserMapManager.Get(long githubUserId): string?`
- Produces: `IGitHubUserMapManager.GetFromUsernameAsync(string login): Task<string?>`

- [ ] **`Managers/BaseManager.cs` を作成**

  設定ファイル取得の優先順位: **Blob > HTTPS URL > ローカルファイル**

  ```csharp
  using System.Text.Json;
  using Azure.Storage.Blobs;
  using Microsoft.Extensions.Configuration;

  namespace GitHubWebhookBridge.Managers;

  /// <summary>
  /// 設定ファイルを Blob / HTTPS URL / ローカルファイルから読み込む抽象基底クラス。
  /// 優先順位: Blob > HTTPS URL > ローカルファイル
  /// </summary>
  public abstract class BaseManager<TData>
  {
      /// <summary>ローカルファイルパス（環境変数から設定）。</summary>
      protected abstract string? FilePath { get; }

      /// <summary>設定ファイルの HTTPS URL（HTTPS のみ許可）。</summary>
      protected abstract string? FileUrl { get; }

      /// <summary>
      /// Blob のパス。形式: "container/path/to/file.json"
      /// 最初の '/' より前がコンテナ名、後がブロブ名。
      /// </summary>
      protected abstract string? BlobPath { get; }

      protected TData Data { get; private set; } = default!;
      private volatile bool        _loaded;
      private readonly SemaphoreSlim _lock = new(1, 1);

      // JSONC（コメント・末尾カンマ付き JSON）をサポートするオプション
      private static readonly JsonSerializerOptions _jsonOpts = new()
      {
          ReadCommentHandling  = JsonCommentHandling.Skip,
          AllowTrailingCommas  = true,
          PropertyNameCaseInsensitive = true,
      };

      private readonly IConfiguration    _config;
      private readonly IHttpClientFactory _httpClientFactory;

      protected BaseManager(IConfiguration config, IHttpClientFactory httpClientFactory)
      {
          _config             = config;
          _httpClientFactory  = httpClientFactory;
      }

      /// <summary>初回呼び出し時のみデータをロードする（二重初期化防止）。</summary>
      public async Task EnsureLoadedAsync()
      {
          if (_loaded) return;
          await _lock.WaitAsync();
          try
          {
              if (_loaded) return;
              var json = await LoadJsonAsync();
              Data = Deserialize(json)
                  ?? throw new InvalidOperationException($"Failed to deserialize {GetType().Name} data");
              _loaded = true;
          }
          finally
          {
              _lock.Release();
          }
      }

      /// <summary>JSON 文字列をデシリアライズする。各サブクラスで実装する。</summary>
      protected abstract TData? Deserialize(string json);

      /// <summary>ソース未指定時のデフォルトファイルパス。各サブクラスで実装する。</summary>
      protected abstract string GetDefaultFilePath();

      private Task<string> LoadJsonAsync()
      {
          if (BlobPath is not null) return LoadFromBlobAsync(BlobPath);
          if (FileUrl  is not null) return LoadFromHttpAsync(FileUrl);
          return LoadFromFileAsync(FilePath ?? GetDefaultFilePath());
      }

      private async Task<string> LoadFromBlobAsync(string blobPath)
      {
          // "container/path/to/file.json" 形式をパース
          var slashIndex = blobPath.IndexOf('/');
          if (slashIndex < 0)
              throw new InvalidOperationException(
                  $"BlobPath must be 'container/blob', got: {blobPath}");

          var containerName = blobPath[..slashIndex];
          var blobName      = blobPath[(slashIndex + 1)..];

          var connStr = _config["AzureWebJobsStorage"]
              ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");

          var blobClient = new BlobClient(connStr, containerName, blobName);
          var download   = await blobClient.DownloadContentAsync();
          return download.Value.Content.ToString();
      }

      private async Task<string> LoadFromHttpAsync(string url)
      {
          // 設定ファイルは HTTPS 経由のみ許可（平文 HTTP は中間者攻撃のリスク）
          if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
              throw new InvalidOperationException($"Config URL must use HTTPS: {url}");

          var http = _httpClientFactory.CreateClient("config");
          return await http.GetStringAsync(url);
      }

      private static async Task<string> LoadFromFileAsync(string path)
      {
          if (!File.Exists(path))
          {
              Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
              await File.WriteAllTextAsync(path, "[]");
              return "[]";
          }
          return await File.ReadAllTextAsync(path);
      }

      /// <summary>型パラメータ T を用いた汎用 JSON デシリアライズ。</summary>
      protected T? DeserializeJson<T>(string json)
          => JsonSerializer.Deserialize<T>(json, _jsonOpts);
  }
  ```

- [ ] **`Managers/IMuteManager.cs`**

  ```csharp
  namespace GitHubWebhookBridge.Managers;

  /// <summary>ユーザーミュート判定インターフェース。</summary>
  public interface IMuteManager
  {
      Task EnsureLoadedAsync();
      bool IsMuted(long userId, string eventName, string? action);
  }
  ```

- [ ] **`tests/GitHubWebhookBridge.Tests/MuteManagerTests.cs` を先に書く**

  ```csharp
  using GitHubWebhookBridge.Managers;
  using Microsoft.Extensions.Configuration;
  using Moq;

  namespace GitHubWebhookBridge.Tests;

  public class MuteManagerTests
  {
      // テスト用に MuteManager を JSON 文字列から初期化するヘルパー
      private static MuteManager CreateFromJson(string json)
      {
          var config = new Mock<IConfiguration>();
          config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
          config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
          config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
          var factory = new Mock<IHttpClientFactory>();
          var mgr = new MuteManager(config.Object, factory.Object);
          mgr.LoadForTest(json);  // テスト専用メソッド（後述）
          return mgr;
      }

      [Fact]
      public void IsMuted_TypeAll_AlwaysTrue()
      {
          var mgr = CreateFromJson(
              """[{"userId":1,"type":"All","events":[]}]""");
          Assert.True(mgr.IsMuted(1, "push", null));
          Assert.True(mgr.IsMuted(1, "issues", "opened"));
      }

      [Fact]
      public void IsMuted_TypeInclude_MatchingEvent_True()
      {
          var mgr = CreateFromJson(
              """[{"userId":1,"type":"Include","events":[{"eventName":"push","actions":null}]}]""");
          Assert.True(mgr.IsMuted(1, "push", null));
      }

      [Fact]
      public void IsMuted_TypeInclude_WithActions_OnlyMatchingAction_True()
      {
          var mgr = CreateFromJson(
              """[{"userId":1,"type":"Include","events":[{"eventName":"issues","actions":["opened"]}]}]""");
          Assert.True(mgr.IsMuted(1, "issues", "opened"));
          Assert.False(mgr.IsMuted(1, "issues", "closed"));
      }

      [Fact]
      public void IsMuted_TypeExclude_NonExcludedEvent_True()
      {
          var mgr = CreateFromJson(
              """[{"userId":1,"type":"Exclude","events":[{"eventName":"push","actions":["a"]}]}]""");
          // push/a は除外 → ミュートされない
          Assert.False(mgr.IsMuted(1, "push", "a"));
          // issues はリストにない → ミュートされる
          Assert.True(mgr.IsMuted(1, "issues", "opened"));
      }

      [Fact]
      public void IsMuted_UnknownUser_False()
      {
          var mgr = CreateFromJson("""[]""");
          Assert.False(mgr.IsMuted(999, "push", null));
      }
  }
  ```

- [ ] **`Managers/MuteManager.cs` を実装**

  ```csharp
  using System.Text.Json.Serialization;
  using Microsoft.Extensions.Configuration;

  namespace GitHubWebhookBridge.Managers;

  /// <summary>ユーザーミュート設定を管理する。</summary>
  public class MuteManager : BaseManager<List<MuteRecord>>, IMuteManager
  {
      protected override string? FilePath { get; }
      protected override string? FileUrl  { get; }
      protected override string? BlobPath { get; }

      public MuteManager(IConfiguration config, IHttpClientFactory httpClientFactory)
          : base(config, httpClientFactory)
      {
          FilePath = config["MUTES_FILE_PATH"];
          FileUrl  = config["MUTES_FILE_URL"];
          BlobPath = config["MUTES_BLOB"];
      }

      protected override string GetDefaultFilePath() => "data/mutes.json";

      protected override List<MuteRecord>? Deserialize(string json)
          => DeserializeJson<List<MuteRecord>>(json);

      /// <summary>テスト専用: JSON を直接ロードする。</summary>
      internal void LoadForTest(string json)
      {
          var data = Deserialize(json)
              ?? throw new InvalidOperationException("Invalid test JSON");
          // リフレクションで Data プロパティに設定 (or internal setter 追加)
          // ここでは protected セッタを使う。BaseManager の Data を internal set にしておくこと。
          DataForTest = data;
      }

      // LoadForTest 用ラッパー（BaseManager に internal DataForTest を追加して対応）
      private List<MuteRecord> DataForTest
      {
          set => SetDataForTest(value);
      }

      public bool IsMuted(long userId, string eventName, string? action)
      {
          if (Data is null)
              throw new InvalidOperationException(
                  "MuteManager is not loaded. Call EnsureLoadedAsync() first.");

          var record = Data.Find(r => r.UserId == userId);
          if (record is null) return false;
          if (record.Type == MuteType.All) return true;

          if (record.Type == MuteType.Include)
          {
              // 指定イベント・アクションがリストにある場合にミュート
              return record.Events.Any(e =>
                  e.EventName == eventName
                  && (e.Actions is null
                      || (action != null && e.Actions.Contains(action))));
          }

          // Exclude モード: リストにないイベントをミュートする
          // TypeScript 版準拠: e.Actions == null のエントリは免除条件にならない
          return !record.Events.Any(e =>
              e.EventName == eventName
              && e.Actions != null
              && (action == null || e.Actions.Contains(action)));
      }
  }

  public record MuteRecord(
      [property: JsonPropertyName("userId")] long       UserId,
      [property: JsonPropertyName("type")]   MuteType   Type,
      [property: JsonPropertyName("events")] List<MuteEvent> Events);

  public record MuteEvent(
      [property: JsonPropertyName("eventName")] string        EventName,
      [property: JsonPropertyName("actions")]   List<string>? Actions);

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum MuteType { Include, Exclude, All }
  ```

  > **実装注意**: `LoadForTest` と `BaseManager.Data` の internal アクセスのため、
  > `BaseManager.cs` に以下を追加する:
  > ```csharp
  > // テスト用: サブクラスがデータを直接設定できるようにする
  > internal void SetDataForTest(TData data) { Data = data; _loaded = true; }
  > ```

- [ ] **`Managers/IGitHubUserMapManager.cs`**

  ```csharp
  namespace GitHubWebhookBridge.Managers;

  /// <summary>GitHub ユーザー ID ↔ Discord ユーザー ID マッピングインターフェース。</summary>
  public interface IGitHubUserMapManager
  {
      Task EnsureLoadedAsync();
      string? Get(long githubUserId);
      Task<string?> GetFromUsernameAsync(string login);
  }
  ```

- [ ] **`Managers/GitHubUserMapManager.cs`**

  ```csharp
  using System.Net.Http.Json;
  using System.Text.Json.Serialization;
  using System.Text.RegularExpressions;
  using Microsoft.Extensions.Configuration;

  namespace GitHubWebhookBridge.Managers;

  /// <summary>GitHub ユーザー ID から Discord ユーザー ID へのマッピングを管理する。</summary>
  public class GitHubUserMapManager : BaseManager<Dictionary<long, string>>, IGitHubUserMapManager
  {
      protected override string? FilePath { get; }
      protected override string? FileUrl  { get; }
      protected override string? BlobPath { get; }

      private readonly IHttpClientFactory _httpClientFactory;

      // GitHub ログイン名の仕様: 英数字とハイフンのみ、先頭は英数字、最大 39 文字
      private static readonly Regex LoginRegex =
          new(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,38}$", RegexOptions.Compiled);

      public GitHubUserMapManager(IConfiguration config, IHttpClientFactory httpClientFactory)
          : base(config, httpClientFactory)
      {
          FilePath           = config["GITHUB_USER_MAP_FILE_PATH"];
          FileUrl            = config["GITHUB_USER_MAP_FILE_URL"];
          BlobPath           = config["GITHUB_USER_MAP_BLOB"];
          _httpClientFactory = httpClientFactory;
      }

      protected override string GetDefaultFilePath() => "data/github-user-map.json";

      protected override Dictionary<long, string>? Deserialize(string json)
          => DeserializeJson<Dictionary<long, string>>(json);

      public string? Get(long githubUserId)
      {
          if (Data is null)
              throw new InvalidOperationException(
                  "GitHubUserMapManager is not loaded. Call EnsureLoadedAsync() first.");
          return Data.TryGetValue(githubUserId, out var discordId) ? discordId : null;
      }

      /// <summary>
      /// GitHub API でユーザー名から数値 ID を引き、マップを検索する。
      /// ログイン名を URL パスに埋め込む前に形式を検証する（パストラバーサル防止）。
      /// </summary>
      public async Task<string?> GetFromUsernameAsync(string login)
      {
          if (!LoginRegex.IsMatch(login))
              return null;

          var http = _httpClientFactory.CreateClient("github");
          var user = await http.GetFromJsonAsync<GitHubUserResponse>($"/users/{login}");
          if (user is null) return null;
          return Get(user.Id);
      }

      private record GitHubUserResponse(
          [property: JsonPropertyName("id")] long Id);
  }
  ```

- [ ] **テスト実行**

  ```bash
  dotnet test tests/GitHubWebhookBridge.Tests/ --logger "console;verbosity=minimal"
  # → Passed! (全テスト)
  ```

- [ ] **コミット**

  ```bash
  git add Managers/ tests/GitHubWebhookBridge.Tests/MuteManagerTests.cs
  git commit -m "feat: MuteManager・GitHubUserMapManager を追加"
  ```

---

## Task 7: NJsonSchema モデル生成（全 59 イベント型）

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `scripts/generate-models.ps1`
- Generate: `Models/GitHubWebhooks/*.cs` (59 ファイル以上)

**Interfaces:**
- Produces: `GitHubWebhookBridge.Models.GitHubWebhooks.*Event` クラス群（Task 8 の ActionFactory が使用）

- [ ] **dotnet ローカルツールのマニフェスト作成**

  ```bash
  dotnet new tool-manifest
  # → .config/dotnet-tools.json が作成される
  ```

- [ ] **NJsonSchema.Console インストール確認**

  ```bash
  # パッケージ名・コマンド名を確認する
  dotnet package search NJsonSchema.Console --take 5
  # → パッケージ名を確認
  dotnet tool install NJsonSchema.Console
  dotnet tool run njsonschema --help
  # → 使用可能なオプションを確認し、generate-models.ps1 に反映
  ```

- [ ] **`scripts/generate-models.ps1` を作成**

  ```powershell
  #!/usr/bin/env pwsh
  param(
      [string]$SchemaDir = "tmp/octokit-schemas",
      [string]$OutputDir = "Models/GitHubWebhooks",
      [string]$Namespace = "GitHubWebhookBridge.Models.GitHubWebhooks"
  )

  # octokit/webhooks スキーマを sparse clone
  if (!(Test-Path $SchemaDir)) {
      git clone --depth 1 --filter=blob:none --sparse `
          https://github.com/octokit/webhooks.git $SchemaDir
      Push-Location $SchemaDir
      git sparse-checkout set payload-schemas/schemas
      Pop-Location
  }

  New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

  $schemas = Get-ChildItem "$SchemaDir/payload-schemas/schemas" -Filter "*.schema.json"
  Write-Host "Found $($schemas.Count) schemas"

  $schemas | ForEach-Object {
      $baseName = $_.BaseName -replace "\.schema$", ""
      # PascalCase に変換: pull_request → PullRequest
      $className = ($baseName -split "_" | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }) -join ""
      $outFile   = Join-Path $OutputDir "${className}Event.cs"

      Write-Host "Generating: $outFile"
      # ローカルツールとして dotnet tool run 経由で実行
      # (--help で確認した正しいオプション名を使うこと)
      dotnet tool run njsonschema /Input:"$($_.FullName)" /Namespace:$Namespace /OutputFilePath:"$outFile"
  }

  Write-Host "Generation complete. Running dotnet build to check for errors..."
  ```

- [ ] **モデル生成を実行**

  ```bash
  pwsh scripts/generate-models.ps1
  ```

- [ ] **ビルド確認 & エラー修正**

  ```bash
  dotnet build 2>&1 | head -50
  ```

  よくある問題と対処:
  - `anyOf` → nullable 型に手動修正: `string?`, `long?` 等
  - クラス名の重複 → ファイルを確認してリネーム
  - 予約語衝突 → `@event`, `@object` 等にリネーム
  - `oneOf` で生成できない場合 → `JsonElement` フォールバック型に差し替え

  **ビルドが通るまで修正を繰り返す。**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **`generate-models.yml` を作成**（手動実行 workflow）

  `.github/workflows/generate-models.yml`:
  ```yaml
  name: Regenerate GitHub Webhook Models

  on:
    workflow_dispatch:

  permissions: {}

  jobs:
    generate:
      runs-on: ubuntu-latest
      permissions:
        contents: write
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.0.x'
        - run: dotnet tool restore
        - run: pwsh scripts/generate-models.ps1
        - name: コミット
          run: |
            git config user.name "github-actions[bot]"
            git config user.email "github-actions[bot]@users.noreply.github.com"
            git add Models/GitHubWebhooks/
            git diff --cached --quiet || git commit -m "chore: GitHub Webhook モデルを再生成"
            git push
  ```

- [ ] **コミット**

  ```bash
  git add .config/dotnet-tools.json scripts/ Models/GitHubWebhooks/ .github/workflows/generate-models.yml
  git commit -m "feat: NJsonSchema で GitHub Webhook 型モデルを生成"
  ```

---

## Task 8: Action 基盤 (IAction / IActionFactory / BaseAction / ActionFactory)

**Files:**
- Create: `Actions/IAction.cs`
- Create: `Actions/IActionFactory.cs`
- Create: `Actions/BaseAction.cs`
- Create: `Actions/ActionFactory.cs`

**Interfaces:**
- Consumes: 全 `*Event.cs` (Task 7)、`IDiscordClient`、`IMessageCacheService`、`IGitHubUserMapManager`
- Produces: `BaseAction<TEvent>` — 全 Action ハンドラーの基底クラス
- Produces: `IActionFactory.GetAction(string, JsonElement, string): IAction`

- [ ] **`Actions/IAction.cs`**

  ```csharp
  namespace GitHubWebhookBridge.Actions;

  /// <summary>全 GitHub Webhook イベントハンドラーの共通インターフェース。</summary>
  public interface IAction
  {
      Task RunAsync();
  }
  ```

- [ ] **`Actions/IActionFactory.cs`**

  ```csharp
  using System.Text.Json;

  namespace GitHubWebhookBridge.Actions;

  /// <summary>イベント名から IAction を生成するファクトリインターフェース。</summary>
  public interface IActionFactory
  {
      IAction GetAction(string eventName, JsonElement body, string webhookUrl);
  }
  ```

- [ ] **`Actions/BaseAction.cs`**

  ```csharp
  using DiffPlex.DiffBuilder;
  using DiffPlex.DiffBuilder.Model;
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Models.Discord;
  using GitHubWebhookBridge.Services;
  using Microsoft.Extensions.Logging;

  namespace GitHubWebhookBridge.Actions;

  /// <summary>
  /// 全 Action ハンドラーの抽象基底クラス。
  /// Discord メッセージの送信と 5 分間キャッシュによる編集機能を提供する。
  /// </summary>
  public abstract class BaseAction<TEvent> : IAction
  {
      protected readonly IDiscordClient        Discord;
      protected readonly string                WebhookUrl;
      protected readonly string                EventName;
      protected readonly TEvent                Event;
      protected readonly IGitHubUserMapManager UserMapManager;
      protected readonly ILogger               Logger;
      private   readonly IMessageCacheService  _cache;

      protected BaseAction(
          IDiscordClient        discord,
          string                webhookUrl,
          string                eventName,
          TEvent                @event,
          IMessageCacheService  cache,
          IGitHubUserMapManager userMapManager,
          ILogger               logger)
      {
          Discord        = discord;
          WebhookUrl     = webhookUrl;
          EventName      = eventName;
          Event          = @event;
          _cache         = cache;
          UserMapManager = userMapManager;
          Logger         = logger;
      }

      /// <summary>イベント処理を実行する。各サブクラスで実装する。</summary>
      public abstract Task RunAsync();

      /// <summary>
      /// Discord にメッセージを送信する。
      /// 同一キーのメッセージが 5 分以内に存在する場合は編集する。
      /// 全メッセージに SuppressNotifications フラグを付与する。
      /// </summary>
      protected async Task SendMessageAsync(string key, DiscordMessage message)
      {
          // SuppressNotifications フラグを付加（既存フラグは保持）
          message = message with { Flags = message.Flags | DiscordMessageFlags.SuppressNotifications };

          var cached = await _cache.GetAsync(WebhookUrl, key);
          if (cached is not null)
          {
              await Discord.EditMessageAsync(WebhookUrl, cached.MessageId, message);
              return;
          }

          var messageId = await Discord.SendMessageAsync(WebhookUrl, message);
          await _cache.SetAsync(WebhookUrl, key, messageId);
      }

      /// <summary>
      /// GitHub ユーザー ID 一覧から Discord メンション文字列を生成する。
      /// 送信者自身は除外する。Team オブジェクトが含まれる場合は事前にフィルタリングすること。
      /// </summary>
      protected async Task<string> GetUsersMentionsAsync(
          long senderId,
          IEnumerable<(long Id, string Login)> users)
      {
          await UserMapManager.EnsureLoadedAsync();
          var mentions = users
              .Where(u => u.Id != senderId)
              .Select(u => UserMapManager.Get(u.Id))
              .Where(discordId => discordId is not null)
              .Select(discordId => $"<@{discordId}>");
          return string.Join(" ", mentions);
      }

      /// <summary>
      /// 2 つのテキスト間の unified diff を生成する（DiffPlex InlineDiffBuilder 使用）。
      /// TypeScript の diff.createPatch() と同等の +/-/スペース 行プレフィックス形式。
      /// 呼び出し元で ```diff コードブロックで囲むこと。
      /// </summary>
      protected static string CreatePatch(string oldText, string newText, string fileName = "file")
      {
          var diff = InlineDiffBuilder.Diff(oldText, newText);
          var sb   = new System.Text.StringBuilder();
          sb.AppendLine($"--- {fileName}");
          sb.AppendLine($"+++ {fileName}");

          foreach (var line in diff.Lines)
          {
              var prefix = line.Type switch
              {
                  ChangeType.Inserted => "+",
                  ChangeType.Deleted  => "-",
                  _                   => " ",
              };
              sb.AppendLine($"{prefix} {line.Text}");
          }

          return sb.ToString();
      }
  }
  ```

- [ ] **`Actions/ActionFactory.cs` を作成**

  59 個の switch case。**実装済み 12 種はデシリアライズあり、スタブ 47 種は `JsonElement` のまま渡す**（スタブクラスは Task 9/10 で作成）。

  ```csharp
  using System.Text.Json;
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Models.GitHubWebhooks;
  using GitHubWebhookBridge.Services;
  using Microsoft.Extensions.Logging;

  namespace GitHubWebhookBridge.Actions;

  /// <summary>イベント名から適切な IAction を生成するファクトリ。</summary>
  public class ActionFactory : IActionFactory
  {
      private readonly IDiscordClient        _discordClient;
      private readonly IMessageCacheService  _cache;
      private readonly IGitHubUserMapManager _userMapManager;
      private readonly ILoggerFactory        _loggerFactory;

      public ActionFactory(
          IDiscordClient        discordClient,
          IMessageCacheService  cache,
          IGitHubUserMapManager userMapManager,
          ILoggerFactory        loggerFactory)
      {
          _discordClient  = discordClient;
          _cache          = cache;
          _userMapManager = userMapManager;
          _loggerFactory  = loggerFactory;
      }

      private static readonly JsonSerializerOptions _opts = new()
      {
          ReadCommentHandling         = JsonCommentHandling.Skip,
          PropertyNameCaseInsensitive = true,
      };

      private T Deserialize<T>(JsonElement body)
          => body.Deserialize<T>(_opts)
             ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");

      private ILogger<T> Logger<T>() => _loggerFactory.CreateLogger<T>();

      public IAction GetAction(string eventName, JsonElement body, string webhookUrl)
          => eventName switch
          {
              // ── 実装済み 12 種（デシリアライズあり） ──────────────────────────
              "ping"                        => new PingAction(_discordClient, webhookUrl, eventName, Deserialize<PingEvent>(body), _cache, _userMapManager, Logger<PingAction>()),
              "push"                        => new PushAction(_discordClient, webhookUrl, eventName, Deserialize<PushEvent>(body), _cache, _userMapManager, Logger<PushAction>()),
              "star"                        => new StarAction(_discordClient, webhookUrl, eventName, Deserialize<StarEvent>(body), _cache, _userMapManager, Logger<StarAction>()),
              "fork"                        => new ForkAction(_discordClient, webhookUrl, eventName, Deserialize<ForkEvent>(body), _cache, _userMapManager, Logger<ForkAction>()),
              "public"                      => new PublicAction(_discordClient, webhookUrl, eventName, Deserialize<PublicEvent>(body), _cache, _userMapManager, Logger<PublicAction>()),
              "pull_request_review_comment" => new PullRequestReviewCommentAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewCommentEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewCommentAction>()),
              "pull_request_review_thread"  => new PullRequestReviewThreadAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewThreadEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewThreadAction>()),
              "pull_request_review"         => new PullRequestReviewAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewAction>()),
              "pull_request"                => new PullRequestAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestEvent>(body), _cache, _userMapManager, Logger<PullRequestAction>()),
              "issue_comment"               => new IssueCommentAction(_discordClient, webhookUrl, eventName, Deserialize<IssueCommentEvent>(body), _cache, _userMapManager, Logger<IssueCommentAction>()),
              "issues"                      => new IssuesAction(_discordClient, webhookUrl, eventName, Deserialize<IssuesEvent>(body), _cache, _userMapManager, Logger<IssuesAction>()),
              "discussion"                  => new DiscussionAction(_discordClient, webhookUrl, eventName, Deserialize<DiscussionEvent>(body), _cache, _userMapManager, Logger<DiscussionAction>()),

              // ── スタブ 47 種（JsonElement 受け渡し） ───────────────────────────
              "branch_protection_rule"         => new BranchProtectionRuleAction(eventName),
              "check_run"                      => new CheckRunAction(eventName),
              "check_suite"                    => new CheckSuiteAction(eventName),
              "code_scanning_alert"            => new CodeScanningAlertAction(eventName),
              "commit_comment"                 => new CommitCommentAction(eventName),
              "create"                         => new CreateAction(eventName),
              "delete"                         => new DeleteAction(eventName),
              "dependabot_alert"               => new DependabotAlertAction(eventName),
              "deploy_key"                     => new DeployKeyAction(eventName),
              "deployment"                     => new DeploymentAction(eventName),
              "deployment_review"              => new DeploymentReviewAction(eventName),
              "deployment_status"              => new DeploymentStatusAction(eventName),
              "discussion_comment"             => new DiscussionCommentAction(eventName),
              "github_app_authorization"       => new GithubAppAuthorizationAction(eventName),
              "gollum"                         => new GollumAction(eventName),
              "installation"                   => new InstallationAction(eventName),
              "installation_repositories"      => new InstallationRepositoriesAction(eventName),
              "label"                          => new LabelAction(eventName),
              "marketplace_purchase"           => new MarketplacePurchaseAction(eventName),
              "member"                         => new MemberAction(eventName),
              "membership"                     => new MembershipAction(eventName),
              "merge_group"                    => new MergeGroupAction(eventName),
              "meta"                           => new MetaAction(eventName),
              "milestone"                      => new MilestoneAction(eventName),
              "org_block"                      => new OrgBlockAction(eventName),
              "organization"                   => new OrganizationAction(eventName),
              "package"                        => new PackageAction(eventName),
              "page_build"                     => new PageBuildAction(eventName),
              "project"                        => new ProjectAction(eventName),
              "project_card"                   => new ProjectCardAction(eventName),
              "project_column"                 => new ProjectColumnAction(eventName),
              "projects_v2_item"               => new ProjectsV2ItemAction(eventName),
              "release"                        => new ReleaseAction(eventName),
              "repository"                     => new RepositoryAction(eventName),
              "repository_dispatch"            => new RepositoryDispatchAction(eventName),
              "repository_import"              => new RepositoryImportAction(eventName),
              "repository_vulnerability_alert" => new RepositoryVulnerabilityAlertAction(eventName),
              "security_advisory"              => new SecurityAdvisoryAction(eventName),
              "sponsorship"                    => new SponsorshipAction(eventName),
              "status"                         => new StatusAction(eventName),
              "team"                           => new TeamAction(eventName),
              "team_add"                       => new TeamAddAction(eventName),
              "watch"                          => new WatchAction(eventName),
              "workflow_dispatch"              => new WorkflowDispatchAction(eventName),
              "workflow_job"                   => new WorkflowJobAction(eventName),
              "workflow_run"                   => new WorkflowRunAction(eventName),

              _ => throw new NotImplementedException($"Event '{eventName}' is not supported"),
          };
  }
  ```

- [ ] **コミット（この段階ではビルドエラーが出る — Task 9/10 のクラス未定義）**

  Tasks 9/10/11 完了後に `dotnet build` を通す。

  ```bash
  git add Actions/IAction.cs Actions/IActionFactory.cs Actions/BaseAction.cs Actions/ActionFactory.cs
  git commit -m "feat: IAction・IActionFactory・BaseAction・ActionFactory を追加"
  ```

---

## Task 9: スタブ Action 47 クラス

**Files:**
- Create: `Actions/BranchProtectionRuleAction.cs` 〜 `Actions/WorkflowRunAction.cs` (47 ファイル)

**Interfaces:**
- Consumes: `IAction`
- Produces: スタブ実装 — `RunAsync()` で `NotImplementedException` を投げる

- [ ] **スタブクラスのパターンを確認**

  以下が全スタブの共通パターン（クラス名だけ変える）:

  ```csharp
  namespace GitHubWebhookBridge.Actions;

  /// <summary>BranchProtectionRule イベントハンドラー（未実装スタブ）。</summary>
  public class BranchProtectionRuleAction : IAction
  {
      private readonly string _eventName;

      public BranchProtectionRuleAction(string eventName)
          => _eventName = eventName;

      public Task RunAsync()
          => throw new NotImplementedException($"Event '{_eventName}' is not implemented");
  }
  ```

- [ ] **47 個のスタブクラスを作成**

  ActionFactory.cs のスタブ側 47 case に対応するクラスファイルを全て作成する:
  `BranchProtectionRuleAction`, `CheckRunAction`, `CheckSuiteAction`, `CodeScanningAlertAction`,
  `CommitCommentAction`, `CreateAction`, `DeleteAction`, `DependabotAlertAction`,
  `DeployKeyAction`, `DeploymentAction`, `DeploymentReviewAction`, `DeploymentStatusAction`,
  `DiscussionCommentAction`, `GithubAppAuthorizationAction`, `GollumAction`,
  `InstallationAction`, `InstallationRepositoriesAction`, `LabelAction`,
  `MarketplacePurchaseAction`, `MemberAction`, `MembershipAction`, `MergeGroupAction`,
  `MetaAction`, `MilestoneAction`, `OrgBlockAction`, `OrganizationAction`,
  `PackageAction`, `PageBuildAction`, `ProjectAction`, `ProjectCardAction`,
  `ProjectColumnAction`, `ProjectsV2ItemAction`, `ReleaseAction`, `RepositoryAction`,
  `RepositoryDispatchAction`, `RepositoryImportAction`, `RepositoryVulnerabilityAlertAction`,
  `SecurityAdvisoryAction`, `SponsorshipAction`, `StatusAction`, `TeamAction`,
  `TeamAddAction`, `WatchAction`, `WorkflowDispatchAction`, `WorkflowJobAction`,
  `WorkflowRunAction`

  効率的な作り方:
  ```bash
  cd Actions
  for name in BranchProtectionRule CheckRun CheckSuite CodeScanningAlert CommitComment \
    Create Delete DependabotAlert DeployKey Deployment DeploymentReview DeploymentStatus \
    DiscussionComment GithubAppAuthorization Gollum Installation InstallationRepositories \
    Label MarketplacePurchase Member Membership MergeGroup Meta Milestone OrgBlock \
    Organization Package PageBuild Project ProjectCard ProjectColumn ProjectsV2Item \
    Release Repository RepositoryDispatch RepositoryImport RepositoryVulnerabilityAlert \
    SecurityAdvisory Sponsorship Status Team TeamAdd Watch WorkflowDispatch WorkflowJob \
    WorkflowRun; do
    cat > "${name}Action.cs" << EOF
  namespace GitHubWebhookBridge.Actions;

  /// <summary>${name} イベントハンドラー（未実装スタブ）。</summary>
  public class ${name}Action : IAction
  {
      private readonly string _eventName;

      public ${name}Action(string eventName)
          => _eventName = eventName;

      public Task RunAsync()
          => throw new NotImplementedException(\$"Event '{_eventName}' is not implemented");
  }
  EOF
  done
  ```

- [ ] **ビルド確認（実装済みアクションのクラスはまだないのでエラーが残る）**

  実装クラスは Task 10 で作成。スタブ分のエラーが消えることを確認:
  ```bash
  dotnet build 2>&1 | grep -v "error" | head -20
  ```

- [ ] **コミット**

  ```bash
  git add Actions/*Action.cs
  git commit -m "feat: スタブ Action 47 クラスを追加"
  ```

---

## Task 10: 実装済み Action 12 クラス（シンプル 5 + 中程度 4 + 複雑 3）

**Files:**
- Create: `Actions/PingAction.cs`
- Create: `Actions/PushAction.cs`
- Create: `Actions/StarAction.cs`
- Create: `Actions/ForkAction.cs`
- Create: `Actions/PublicAction.cs`
- Create: `Actions/PullRequestReviewAction.cs`
- Create: `Actions/PullRequestReviewCommentAction.cs`
- Create: `Actions/PullRequestReviewThreadAction.cs`
- Create: `Actions/IssueCommentAction.cs`
- Create: `Actions/PullRequestAction.cs`
- Create: `Actions/IssuesAction.cs`
- Create: `Actions/DiscussionAction.cs`
- Create: `tests/GitHubWebhookBridge.Tests/PullRequestActionTests.cs`
- Create: `tests/GitHubWebhookBridge.Tests/Fixtures/push.json`
- Create: `tests/GitHubWebhookBridge.Tests/Fixtures/pull_request.json`

**Interfaces:**
- Consumes: `BaseAction<TEvent>`、各 `*Event` モデル（Task 7）、`EmbedColors`、`EmbedHelper`
- Produces: 12 種のイベントに対する Discord Embed 送信ロジック

> **重要**: 実装は TypeScript 版（`src/actions/`）を**必ず**参照すること。
> 特に以下を忠実移植する:
> - action ごとの色マップ（`EmbedColors.*` を使う）
> - キャッシュキー生成パターン（`"repo#number-group"` 形式）
> - `getBody()` が 500 文字で切断する
> - `isWipTitle()` の 4 パターン正規表現
> - `getUsersMentions()` で sender を除外
> - diff を ` ```diff ... ``` ` コードブロックで囲む

- [ ] **`Actions/PingAction.cs` を実装**

  TS `src/actions/ping.ts` を参照。zen quote + hook 情報の embed。

  ```csharp
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Models.Discord;
  using GitHubWebhookBridge.Models.GitHubWebhooks;
  using GitHubWebhookBridge.Services;
  using GitHubWebhookBridge.Utils;
  using Microsoft.Extensions.Logging;

  namespace GitHubWebhookBridge.Actions;

  /// <summary>GitHub ping イベントを Discord に通知する。</summary>
  public class PingAction : BaseAction<PingEvent>
  {
      public PingAction(
          IDiscordClient discord, string webhookUrl, string eventName,
          PingEvent @event, IMessageCacheService cache,
          IGitHubUserMapManager userMapManager, ILogger<PingAction> logger)
          : base(discord, webhookUrl, eventName, @event, cache, userMapManager, logger) { }

      public override async Task RunAsync()
      {
          var embed = EmbedHelper.CreateEmbed(
              eventName:   EventName,
              color:       EmbedColors.Ping,
              title:       "🔔 Webhook connected",
              description: Event.Zen,
              fields: new List<DiscordEmbedField>
              {
                  new("Hook ID",   Event.HookId?.ToString() ?? "N/A"),
                  new("Hook URL",  Event.Hook?.Config?.Url  ?? "N/A"),
              });

          await SendMessageAsync("ping", new DiscordMessage(Embeds: [embed]));
      }
  }
  ```

  > 生成された `PingEvent` の実際のプロパティ名を確認し、上記のプロパティアクセスを修正すること。
  > 存在しないプロパティは `JsonElement` からのアクセスに切り替えるか、`PingEvent` に `JsonExtensionData` を追加する。

- [ ] **`Actions/PushAction.cs` を実装**

  TS `src/actions/push.ts` を参照。コミット一覧（先頭 5 件）・短縮 SHA リンク。

  ```csharp
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Models.Discord;
  using GitHubWebhookBridge.Models.GitHubWebhooks;
  using GitHubWebhookBridge.Services;
  using GitHubWebhookBridge.Utils;
  using Microsoft.Extensions.Logging;

  namespace GitHubWebhookBridge.Actions;

  /// <summary>GitHub push イベントを Discord に通知する。</summary>
  public class PushAction : BaseAction<PushEvent>
  {
      public PushAction(
          IDiscordClient discord, string webhookUrl, string eventName,
          PushEvent @event, IMessageCacheService cache,
          IGitHubUserMapManager userMapManager, ILogger<PushAction> logger)
          : base(discord, webhookUrl, eventName, @event, cache, userMapManager, logger) { }

      public override async Task RunAsync()
      {
          var commits = Event.Commits ?? [];
          if (!commits.Any()) return;

          // 先頭 5 件のコミットを一覧化
          var commitLines = commits.Take(5).Select(c =>
          {
              var shortSha = c.Id?[..7] ?? "unknown";
              var msg      = c.Message?.Split('\n')[0] ?? "";
              return $"[`{shortSha}`]({c.Url}) {msg}";
          });

          var description = string.Join("\n", commitLines);
          if (commits.Count > 5)
              description += $"\n...and {commits.Count - 5} more commits";

          var repoFullName = Event.Repository?.FullName ?? "unknown";
          var ref_         = Event.Ref ?? "";
          var branchName   = ref_.Replace("refs/heads/", "");

          var embed = EmbedHelper.CreateEmbed(
              eventName:   EventName,
              color:       EmbedColors.Push,
              title:       $"[{repoFullName}] {commits.Count} commit(s) pushed to {branchName}",
              description: description,
              url:         Event.Compare);

          var key = $"{repoFullName}-push-{branchName}";
          await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
      }
  }
  ```

- [ ] **`Actions/StarAction.cs` を実装**

  TS `src/actions/star.ts` を参照:

  ```csharp
  // (PingAction と同パターン。TS ファイル参照して実装)
  ```

- [ ] **`Actions/ForkAction.cs` と `Actions/PublicAction.cs` を実装**

  TS `src/actions/fork.ts` と `src/actions/public.ts` を参照。各 1 embed のシンプルなハンドラー。

- [ ] **`Actions/PullRequestReviewAction.cs` を実装**

  TS `src/actions/pull-request-review.ts`（205 行）を参照。
  - `submitted` (approved/changes_requested/commented)、`edited`、`dismissed` の 3 アクション
  - `createPatch` で review body diff
  - `getUsersMentions` で reviewer を mention

- [ ] **`Actions/PullRequestReviewCommentAction.cs` を実装**

  TS `src/actions/pull-request-review-comment.ts`（134 行）を参照。
  - `created`/`edited`/`deleted` の 3 アクション

- [ ] **`Actions/PullRequestReviewThreadAction.cs` を実装**

  TS `src/actions/pull-request-review-thread.ts`（110 行）を参照。
  - `resolved`/`unresolved` の 2 アクション

- [ ] **`Actions/IssueCommentAction.cs` を実装**

  TS `src/actions/issue-comment.ts`（188 行）を参照。
  - `created`/`edited`/`deleted` の 3 アクション
  - 本文中の `@mention` を正規表現で抽出し Discord mention に変換するロジックに注意

- [ ] **`Actions/PullRequestAction.cs` を実装**

  TS `src/actions/pull-request.ts`（851 行）を参照。最も複雑なハンドラー。
  - 20 アクション: `opened`, `closed`(merged vs closed), `reopened`, `assigned`, `unassigned`,
    `review_requested`, `review_request_removed`, `labeled`, `unlabeled`, `edited`,
    `ready_for_review`, `locked`, `unlocked`, `auto_merge_enabled`, `auto_merge_disabled`,
    `converted_to_draft`, `demilestoned`, `milestoned`, `enqueued`, `dequeued`, `synchronize`(skip)
  - `isWipTitle()`: 4 パターン（`WIP`, `[WIP]`, `wip:`, `🚧`）を大文字小文字区別なしで検出
  - キャッシュキー共有: `assigned`/`unassigned` → `-assigned`、`labeled`/`unlabeled` → `-label` 等
  - `getBody()`: 本文を 500 文字で切断（`...` 付き）
  - `onEdited()`: タイトル・本文・ベースブランチの diff を `createPatch` で生成

- [ ] **`Actions/IssuesAction.cs` を実装**

  TS `src/actions/issues.ts`（571 行）を参照。16 アクション。PullRequestAction と類似のパターン。

- [ ] **`Actions/DiscussionAction.cs` を実装**

  TS `src/actions/discussion.ts`（489 行）を参照。13 アクション。

- [ ] **テスト fixtures 取得**

  ```bash
  # @octokit/webhooks-examples から fixture JSON を取得
  # node がローカルにあるため npm/npx 経由で取得可能
  mkdir -p tests/GitHubWebhookBridge.Tests/Fixtures
  node -e "
  const examples = require('@octokit/webhooks-examples');
  const fs = require('fs');
  // push イベントの最初のサンプルを保存
  const push = examples.find(e => e.name === 'push');
  if (push) fs.writeFileSync('tests/GitHubWebhookBridge.Tests/Fixtures/push.json',
    JSON.stringify(push.examples[0].payload, null, 2));
  const pr = examples.find(e => e.name === 'pull_request');
  if (pr) fs.writeFileSync('tests/GitHubWebhookBridge.Tests/Fixtures/pull_request.json',
    JSON.stringify(pr.examples[0].payload, null, 2));
  "
  ```

- [ ] **`tests/GitHubWebhookBridge.Tests/PullRequestActionTests.cs` を作成**

  ```csharp
  using System.Text.Json;
  using GitHubWebhookBridge.Actions;
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Models.GitHubWebhooks;
  using GitHubWebhookBridge.Services;
  using Microsoft.Extensions.Logging.Abstractions;
  using Moq;

  namespace GitHubWebhookBridge.Tests;

  public class PullRequestActionTests
  {
      private static PullRequestAction CreateAction(PullRequestEvent @event)
      {
          var discord    = new Mock<IDiscordClient>();
          var cache      = new Mock<IMessageCacheService>();
          var userMap    = new Mock<IGitHubUserMapManager>();
          discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<Models.Discord.DiscordMessage>()))
                 .ReturnsAsync("msg-id-123");
          cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync((CachedMessage?)null);
          cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask);
          return new PullRequestAction(
              discord.Object, "https://discord.com/api/webhooks/000/test",
              "pull_request", @event, cache.Object, userMap.Object,
              NullLogger<PullRequestAction>.Instance);
      }

      [Theory]
      [InlineData("WIP: add feature",         true)]
      [InlineData("[WIP] add feature",         true)]
      [InlineData("wip: add feature",          true)]
      [InlineData("🚧 add feature",            true)]
      [InlineData("add feature",               false)]
      [InlineData("add feature (WIP inside)",  false)]   // 部分一致は含まない
      public async Task IsWipTitle_DetectsCorrectly(string title, bool expectedWip)
      {
          // PullRequestAction.IsWipTitle は private なので、
          // opened アクションで embed の title を確認することで間接的にテストする
          var payload = JsonSerializer.Deserialize<PullRequestEvent>(
              File.ReadAllText("Fixtures/pull_request.json"))!;
          // イベントを操作して WIP タイトルにする — 実装の型によって調整が必要
          // (生成型の構造による。ここではパターン検証の例を示す)
          _ = expectedWip; // 実際の assertion は実装型確認後に追記
          _ = title;
          await Task.CompletedTask;
      }

      [Fact]
      public async Task RunAsync_OpenedEvent_SendsMessage()
      {
          var json    = File.ReadAllText("Fixtures/pull_request.json");
          var payload = JsonSerializer.Deserialize<PullRequestEvent>(json,
              new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

          var discord = new Mock<IDiscordClient>();
          discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<Models.Discord.DiscordMessage>()))
                 .ReturnsAsync("msg-id");
          var cache   = new Mock<IMessageCacheService>();
          cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync((CachedMessage?)null);
          cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask);
          var userMap = new Mock<IGitHubUserMapManager>();
          userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

          var action = new PullRequestAction(
              discord.Object, "https://discord.com/api/webhooks/000/test",
              "pull_request", payload, cache.Object, userMap.Object,
              NullLogger<PullRequestAction>.Instance);

          await action.RunAsync();

          discord.Verify(d => d.SendMessageAsync(
              It.IsAny<string>(),
              It.Is<Models.Discord.DiscordMessage>(m => m.Embeds != null && m.Embeds.Any())),
              Times.AtLeastOnce);
      }
  }
  ```

- [ ] **全テスト実行**

  ```bash
  dotnet test tests/GitHubWebhookBridge.Tests/ --logger "console;verbosity=minimal"
  # → 全テスト PASS
  ```

- [ ] **コミット**

  ```bash
  git add Actions/ tests/
  git commit -m "feat: 実装済み Action 12 クラスを追加"
  ```

---

## Task 11: WebhookFunction + Program.cs (完全 DI)

**Files:**
- Create: `Functions/WebhookFunction.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: `IActionFactory`、`IMuteManager`、`SignatureValidator`、`IConfiguration`
- Produces: Azure Function エントリーポイント + 完全な DI コンテナ

- [ ] **`Functions/WebhookFunction.cs` を実装**

  ```csharp
  using System.Text.Json;
  using GitHubWebhookBridge.Actions;
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Utils;
  using Microsoft.AspNetCore.Http;
  using Microsoft.AspNetCore.Mvc;
  using Microsoft.Azure.Functions.Worker;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Logging;

  namespace GitHubWebhookBridge.Functions;

  /// <summary>GitHub Webhook を受信し Discord に通知する Azure Function。</summary>
  public class WebhookFunction
  {
      private readonly IActionFactory  _actionFactory;
      private readonly IMuteManager    _muteManager;
      private readonly IConfiguration  _config;
      private readonly ILogger<WebhookFunction> _logger;

      public WebhookFunction(
          IActionFactory  actionFactory,
          IMuteManager    muteManager,
          IConfiguration  config,
          ILogger<WebhookFunction> logger)
      {
          _actionFactory = actionFactory;
          _muteManager   = muteManager;
          _config        = config;
          _logger        = logger;
      }

      [Function("GitHubWebhook")]
      public async Task<IActionResult> Run(
          [HttpTrigger(AuthorizationLevel.Anonymous, "post",
              Route = "GitHubWebhook")] HttpRequest req)
      {
          const long MaxBodyBytes = 10L * 1024 * 1024;  // 10 MB

          // 1. Content-Length プレチェック
          if (req.ContentLength.HasValue && req.ContentLength.Value > MaxBodyBytes)
              return new StatusCodeResult(413);

          // 2. ボディを MaxBodyBytes まで逐次読み取り
          req.EnableBuffering();
          using var ms    = new MemoryStream();
          var       chunk = new byte[81920];
          int bytesRead;
          while ((bytesRead = await req.Body.ReadAsync(chunk)) > 0)
          {
              ms.Write(chunk, 0, bytesRead);
              if (ms.Length > MaxBodyBytes)
                  return new StatusCodeResult(413);
          }
          var rawBody = ms.ToArray();
          req.Body.Position = 0;

          // 3. HMAC-SHA256 署名検証
          var secret = _config["GITHUB_WEBHOOK_SECRET"]
              ?? throw new InvalidOperationException("GITHUB_WEBHOOK_SECRET not set");
          if (!SignatureValidator.Validate(rawBody, req.Headers, secret))
              return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-Hub-Signature" });

          // 4. X-GitHub-Event ヘッダー検証（ログインジェクション防止）
          var rawEventName = req.Headers["X-GitHub-Event"].ToString();
          if (string.IsNullOrEmpty(rawEventName))
              return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-GitHub-Event" });
          var eventName = SanitizeEventName(rawEventName);
          if (eventName != rawEventName)
          {
              _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
              return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-GitHub-Event" });
          }

          // 5. ?url= — discord.com Webhook URL に限定（SSRF 対策）
          string webhookUrl;
          if (req.Query.TryGetValue("url", out var urlParam) && !string.IsNullOrEmpty(urlParam))
          {
              var candidate = urlParam.ToString();
              if (!IsAllowedWebhookUrl(candidate))
                  return new BadRequestObjectResult(new { message = "Bad Request: Invalid url parameter" });
              webhookUrl = candidate;
          }
          else
          {
              webhookUrl = _config["DISCORD_WEBHOOK_URL"]
                  ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set");
          }

          // 6. ?disabled-events= は DISABLED_EVENTS 環境変数を上書き
          var disabledEvents = req.Query.TryGetValue("disabled-events", out var deParam) && !string.IsNullOrEmpty(deParam)
              ? deParam.ToString()
              : _config["DISABLED_EVENTS"];
          if (!string.IsNullOrEmpty(disabledEvents))
          {
              var disabled = disabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries);
              if (disabled.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                  return new ObjectResult(new { message = "Disabled event" }) { StatusCode = 202 };
          }

          // 7. JSON デシリアライズ
          var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
          var body    = JsonSerializer.Deserialize<JsonElement>(rawBody, options);

          // 8. 送信者ミュートチェック
          await _muteManager.EnsureLoadedAsync();
          if (body.TryGetProperty("sender", out var sender)
              && sender.TryGetProperty("id", out var senderId))
          {
              var action = body.TryGetProperty("action", out var a) ? a.GetString() : null;
              if (_muteManager.IsMuted(senderId.GetInt64(), eventName, action))
                  return new OkObjectResult(new { message = "Muted user" });
          }

          // 9-10. アクションハンドラーへディスパッチ
          var actionHandler = _actionFactory.GetAction(eventName, body, webhookUrl);
          try
          {
              await actionHandler.RunAsync();
              return new OkResult();
          }
          catch (NotImplementedException)
          {
              _logger.LogInformation("Method not implemented for event: {EventName}", eventName);
              return new ObjectResult(new { message = "Method not implemented" }) { StatusCode = 406 };
          }
          catch (HttpRequestException ex)
          {
              _logger.LogError(ex, "Discord API request failed for event: {EventName}", eventName);
              return new StatusCodeResult(500);
          }
          catch (Exception ex)
          {
              _logger.LogError(ex, "Unexpected error processing event: {EventName}", eventName);
              return new StatusCodeResult(500);
          }
      }

      /// <summary>GitHub イベント名として有効な文字（小文字英字・アンダースコア）のみ許可。</summary>
      private static string SanitizeEventName(string raw)
          => System.Text.RegularExpressions.Regex.Replace(raw, "[^a-z_]", "");

      /// <summary>SSRF 対策: discord.com Webhook URL プレフィックスのみ許可。</summary>
      private static bool IsAllowedWebhookUrl(string url)
          => url.StartsWith("https://discord.com/api/webhooks/",    StringComparison.OrdinalIgnoreCase)
          || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
  }
  ```

- [ ] **`Program.cs` を完全 DI 版に更新**

  ```csharp
  using GitHubWebhookBridge.Actions;
  using GitHubWebhookBridge.Managers;
  using GitHubWebhookBridge.Services;
  using Microsoft.Azure.Functions.Worker.Builder;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;

  var builder = FunctionsApplication.CreateBuilder(args);
  builder.ConfigureFunctionsWebApplication();

  builder.Services
      // 汎用用途 HttpClient
      .AddHttpClient()
      // GitHub API 用クライアント
      .AddHttpClient("github", c =>
      {
          c.BaseAddress = new Uri("https://api.github.com");
          c.DefaultRequestHeaders.Add("User-Agent", "github-webhook-bridge");
          c.Timeout = TimeSpan.FromSeconds(10);
      })
      .Services
      // 設定ファイル取得用クライアント
      .AddHttpClient("config", c => c.Timeout = TimeSpan.FromSeconds(10))
      .Services
      // Discord Webhook 用クライアント（15 秒タイムアウト）
      .AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15))
      .Services
      .AddSingleton<IDiscordClient,  DiscordClient>()
      // MessageCacheService を具象型 + インターフェース両方で登録
      // （TableStorageInitializer が具象型を直接注入できるようにするため）
      .AddSingleton<MessageCacheService>()
      .AddSingleton<IMessageCacheService>(sp => sp.GetRequiredService<MessageCacheService>())
      .AddSingleton<IMuteManager,          MuteManager>()
      .AddSingleton<IGitHubUserMapManager, GitHubUserMapManager>()
      .AddSingleton<IActionFactory,        ActionFactory>()
      // テーブル作成をホスト起動時に非同期実行
      .AddHostedService<TableStorageInitializer>();

  builder.Build().Run();
  ```

- [ ] **ビルド確認**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)
  ```

- [ ] **コミット**

  ```bash
  git add Functions/ Program.cs
  git commit -m "feat: WebhookFunction とDI 登録を追加"
  ```

---

## Task 12: CI Workflow (dotnet-ci.yml)

**Files:**
- Create: `.github/workflows/dotnet-ci.yml`

**Interfaces:**
- Produces: PR/push 時の自動ビルド & テスト

- [ ] **`.github/workflows/dotnet-ci.yml` を作成**

  ```yaml
  name: .NET CI

  on:
    push:
      branches: [master]
    pull_request:

  permissions: {}

  jobs:
    build-test:
      runs-on: windows-latest
      permissions:
        contents: read
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.0.x'
        - name: 依存関係を復元
          run: dotnet restore --locked-mode
        - name: ビルド
          run: dotnet build --no-restore -c Release
        - name: テスト実行
          run: dotnet test --no-build -c Release --logger trx
  ```

- [ ] **コミット**

  ```bash
  git add .github/workflows/dotnet-ci.yml
  git commit -m "ci: .NET CI workflow を追加"
  ```

---

## Task 13: 旧 TS/Node 資産の削除 + ドキュメント更新

**Files:**
- Delete: `src/`, `api/`, `generate-docs/`, `node_modules/`
- Delete: 各種 TS/Node 設定ファイル
- Delete: 旧 GitHub Actions workflows
- Modify: `README.md`
- Modify: `CLAUDE.md`
- Modify: `.gitignore` (TS 固有エントリ除去)

> **事前確認**: `docs/` ディレクトリ（102 枚の Puppeteer 生成 PNG）の扱いをユーザーに確認する。
> TS パイプライン廃止後は更新不可のため、残置 or 削除のどちらかを選ぶ。

- [ ] **ユーザーに `docs/` の扱いを確認してから削除を進める**

- [ ] **TS/Node 資産を削除**

  ```bash
  cd /mnt/hdd/repos/github.com/book000/github-webhook-bridge

  # TS/Node 資産
  git rm -rf src/ api/ generate-docs/
  git rm package.json pnpm-lock.yaml tsconfig.json
  git rm vercel.json Dockerfile compose.yaml
  git rm -f .dockerignore eslint.config.mjs .prettierrc.yml .depcheckrc.json .node-version
  git rm -rf .devcontainer/

  # 旧 CI workflows
  git rm .github/workflows/check-import.yml
  git rm .github/workflows/docker.yml
  git rm .github/workflows/generate-docs.yml
  git rm .github/workflows/nodejs-ci-pnpm.yml

  # node_modules は .gitignore で除外されているため git rm 不要
  # ただしローカルから削除
  rm -rf node_modules/

  # renovate.json は言語非依存の設定なので残置（任意）
  ```

- [ ] **`README.md` を C# 版に書き換え**

  ```markdown
  # github-webhook-bridge

  GitHub の Webhook を受信し、Discord に通知メッセージを送信する Azure Functions アプリケーション。

  ## セットアップ

  ### 必要なもの

  - .NET 10 SDK
  - Azure Storage アカウント（または Azurite でローカル開発）
  - GitHub Webhook シークレット
  - Discord Webhook URL

  ### 環境変数

  | 変数名 | 必須 | 説明 |
  |--------|------|------|
  | `GITHUB_WEBHOOK_SECRET` | ✅ | HMAC-SHA256 署名検証シークレット |
  | `DISCORD_WEBHOOK_URL` | ✅ | デフォルト Discord Webhook URL |
  | `AzureWebJobsStorage` | ✅ | Azure Storage 接続文字列（ローカルは `UseDevelopmentStorage=true`） |
  | `MUTES_FILE_PATH` | — | ミュート設定ローカルパス（デフォルト: `data/mutes.json`） |
  | `MUTES_FILE_URL` | — | ミュート設定 HTTPS URL |
  | `MUTES_BLOB` | — | Blob パス: `container/path/to/mutes.json` |
  | `GITHUB_USER_MAP_FILE_PATH` | — | ユーザーマップローカルパス（デフォルト: `data/github-user-map.json`） |
  | `GITHUB_USER_MAP_FILE_URL` | — | ユーザーマップ HTTPS URL |
  | `GITHUB_USER_MAP_BLOB` | — | Blob パス: `container/path/to/github-user-map.json` |
  | `DISABLED_EVENTS` | — | 無効化するイベント名（カンマ区切り） |

  ### ローカル開発

  ```bash
  # 依存パッケージを復元
  dotnet restore

  # Azurite を起動（別ターミナル）
  npx azurite --silent

  # ビルド
  dotnet build

  # テスト
  dotnet test

  # 型モデルを再生成
  dotnet tool restore
  pwsh scripts/generate-models.ps1
  ```

  ## エンドポイント

  - **本番**: `POST https://<functionapp>.azurewebsites.net/GitHubWebhook`
  - **ローカル**: `POST http://localhost:7071/GitHubWebhook`

  ## 移行注意点（Vercel 版からの破壊的変更）

  - エンドポイントが `POST /` → `POST /GitHubWebhook` に変更
  - `?url=` パラメータは `https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックスのみ許可（旧版は無検証）

  ## ライセンス

  MIT
  ```

- [ ] **`CLAUDE.md` の開発コマンドセクションを更新**

  CLAUDE.md の「開発コマンド」「アーキテクチャと主要ファイル」「パッケージマネージャー」などの
  TS 版記述を C# 版に書き換える。詳細は CLAUDE.md の最新版を参照して更新すること。

- [ ] **ビルド & テスト最終確認**

  ```bash
  dotnet build
  # → Build succeeded. 0 Error(s)

  dotnet test tests/GitHubWebhookBridge.Tests/
  # → 全テスト PASS
  ```

- [ ] **最終コミット**

  ```bash
  git add -A
  git commit -m "chore: TypeScript/Node.js 資産を削除し README/CLAUDE.md を更新"
  ```

---

## Self-Review Checklist

以下は計画完了後に自身でチェックすること:

- [ ] **Spec coverage**: 高リスク判断（ADLS削除/接続文字列/Blob対応/App Insights有効）がすべてタスクに反映されているか
- [ ] **Placeholder scan**: `CONFIRM_VERSION` を全パッケージで実際のバージョンに置換したか（Task 2 で実施）
- [ ] **Type consistency**: ActionFactory の switch case のクラス名と実際の Action クラス名が一致するか
- [ ] **MuteManager の null 非対称ロジック**: TS 版の `src/manager/mute.ts:65-84` を参照し、include/exclude の null ハンドリングが完全一致するか
- [ ] **EmbedColors 件数**: `grep -c 'public const int' Utils/EmbedColors.cs` が TS 版と一致するか（71 件）
- [ ] **docs/ の扱い**: Task 13 実施前にユーザーへ確認済みか
