# Directory Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate from a flat root layout to `src/` + `tests/` sibling directories, adding a `.sln` and `Directory.Build.props`.

**Architecture:** All source files move to `src/` (csproj directly there, no subdirectory). Test files flatten to `tests/` directly (removing the `GitHubWebhookBridge.Tests/` subdirectory). A root-level `Directory.Build.props` centralizes shared MSBuild properties. A `.sln` ties both projects together.

**Tech Stack:** .NET 10, Azure Functions v4 Isolated, xUnit, Stryker.NET, dotnet CLI, git mv

## Global Constraints

- All `git mv` — never plain `mv`. File history must be preserved.
- `RootNamespace` and `AssemblyName` stay `GitHubWebhookBridge` — no namespace changes.
- `dotnet build -c Release` and `dotnet test -c Release` must stay green after every task.
- `packages.lock.json` lives in `src/` after migration; must regenerate via `dotnet restore --force-evaluate` when moved.
- Never `#pragma warning disable` — fix the root cause instead.

---

## File Map

| Current path | After migration | Change type |
|---|---|---|
| `GitHubWebhookBridge.csproj` | `src/GitHubWebhookBridge.csproj` | Move + edit |
| `Program.cs` | `src/Program.cs` | Move |
| `host.json` | `src/host.json` | Move |
| `local.settings.json` | `src/local.settings.json` | Move |
| `packages.lock.json` | `src/packages.lock.json` | Delete + regenerate |
| `stylecop.json` | `src/stylecop.json` | Move |
| `Actions/` | `src/Actions/` | Move (whole tree) |
| `Functions/` | `src/Functions/` | Move (whole tree) |
| `Managers/` | `src/Managers/` | Move (whole tree) |
| `Models/` | `src/Models/` | Move (whole tree) |
| `Services/` | `src/Services/` | Move (whole tree) |
| `Utils/` | `src/Utils/` | Move (whole tree) |
| `tests/GitHubWebhookBridge.Tests/GitHubWebhookBridge.Tests.csproj` | `tests/GitHubWebhookBridge.Tests.csproj` | Move + edit |
| `tests/GitHubWebhookBridge.Tests/*.cs` (18 files) | `tests/*.cs` | Move (flatten) |
| *(new)* | `GitHubWebhookBridge.sln` | Create |
| *(new)* | `Directory.Build.props` | Create |
| `stryker-config.json` | `stryker-config.json` | Edit (paths) |
| `.github/workflows/azure-functions-deploy.yml` | same | Edit (publish path) |
| `CLAUDE.md` | same | Edit (func start) |
| `.editorconfig` | same | Edit (test glob) |

---

## Task 1: Create `Directory.Build.props`

**Files:**
- Create: `Directory.Build.props`

**Interfaces:**
- Produces: shared MSBuild properties consumed by both `src/GitHubWebhookBridge.csproj` and `tests/GitHubWebhookBridge.Tests.csproj`

- [ ] **Step 1: Create `Directory.Build.props` at repo root**

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Verify build still passes**

```bash
dotnet build -c Release
```

Expected: build succeeds (properties are now inherited; having them in both places is redundant but not an error yet — they'll be removed from the individual csproj files in later tasks).

- [ ] **Step 3: Commit**

```bash
git add Directory.Build.props
git commit -m "build: Directory.Build.props で共通 MSBuild プロパティを集約"
```

---

## Task 2: Move source project to `src/`

**Files:**
- Move: all source files (see file map above) → `src/`
- Modify: `src/GitHubWebhookBridge.csproj` (remove duplicated properties, update `<Compile Remove>`)

**Interfaces:**
- Consumes: `Directory.Build.props` from Task 1 (inherits `TargetFramework`, `Nullable`, `ImplicitUsings`)
- Produces: `src/GitHubWebhookBridge.csproj` at new path, consumed by Task 3 (`ProjectReference`) and Task 4 (`.sln`, stryker, deploy)

- [ ] **Step 1: Create `src/` directory and move source trees**

```bash
mkdir src

git mv Actions src/
git mv Functions src/
git mv Managers src/
git mv Models src/
git mv Services src/
git mv Utils src/
git mv Program.cs src/
git mv host.json src/
git mv local.settings.json src/
git mv stylecop.json src/
git mv GitHubWebhookBridge.csproj src/
```

Do NOT move `packages.lock.json` yet — delete it in a later step.

- [ ] **Step 2: Update `src/GitHubWebhookBridge.csproj`**

Open `src/GitHubWebhookBridge.csproj`. Apply all changes below at once.

**Remove** the following properties already covered by `Directory.Build.props`:
```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

**Remove** the `tests/**` compile exclude (tests directory is no longer under `src/`):
```xml
<!-- DELETE this ItemGroup entry: -->
<Compile Remove="tests/**" />
```

The resulting `<ItemGroup>` for `<Compile Remove>` should contain only:
```xml
<ItemGroup>
  <!-- 自動生成モデルはコンパイル対象外（参照用）。使用する場合は using で取り込む -->
  <Compile Remove="Models/GitHubWebhooks/Generated/**" />
</ItemGroup>
```

The `<AdditionalFiles Include="stylecop.json" />` path is correct as-is because `stylecop.json` is now in the same directory as the csproj.

The full file after edits:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <RootNamespace>GitHubWebhookBridge</RootNamespace>
    <AssemblyName>GitHubWebhookBridge</AssemblyName>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <!-- .editorconfig の IDE 診断ルールをビルド時に有効化する -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- 自動生成モデルはコンパイル対象外（参照用）。使用する場合は using で取り込む -->
    <Compile Remove="Models/GitHubWebhooks/Generated/**" />
  </ItemGroup>

  <ItemGroup>
    <!-- テストプロジェクトから internal メンバーへのアクセスを許可する -->
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>GitHubWebhookBridge.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

  <ItemGroup>
    <!-- Azure Functions -->
    <PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.8.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.7" />
    <!-- OpenTelemetry / Azure Monitor -->
    <PackageReference Include="Microsoft.Azure.Functions.Worker.OpenTelemetry" Version="1.2.0" />
    <!-- Azure Storage -->
    <PackageReference Include="Azure.Data.Tables" Version="12.11.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.29.1" />
    <!-- Utilities -->
    <PackageReference Include="DiffPlex" Version="1.9.0" />
    <!-- StyleCop: ビルド時のみ使用（出力には含まれない） -->
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- StyleCop 設定ファイル -->
    <AdditionalFiles Include="stylecop.json" />
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

- [ ] **Step 3: Regenerate `packages.lock.json`**

The old `packages.lock.json` at repo root is now stale (csproj moved). Delete it and regenerate.

```bash
rm packages.lock.json
cd src
dotnet restore --force-evaluate
cd ..
```

Expected: `src/packages.lock.json` is created.

- [ ] **Step 4: Build from repo root to verify**

```bash
dotnet build src/GitHubWebhookBridge.csproj -c Release
```

Expected: build succeeds. (The test project will be wired up via `.sln` in Task 3.)

- [ ] **Step 5: Commit**

```bash
git add src/ packages.lock.json
git commit -m "refactor: メインプロジェクトを src/ へ移動"
```

---

## Task 3: Flatten test project to `tests/`

**Files:**
- Move: `tests/GitHubWebhookBridge.Tests/*.cs` → `tests/*.cs` (18 files)
- Move: `tests/GitHubWebhookBridge.Tests/GitHubWebhookBridge.Tests.csproj` → `tests/GitHubWebhookBridge.Tests.csproj`
- Modify: `tests/GitHubWebhookBridge.Tests.csproj` (update `ProjectReference`, remove duplicated properties)
- Create: `GitHubWebhookBridge.sln`
- Modify: `.editorconfig` (update `[tests/**/*.cs]` glob)

**Interfaces:**
- Consumes: `src/GitHubWebhookBridge.csproj` from Task 2
- Produces: `tests/GitHubWebhookBridge.Tests.csproj` at new path; `GitHubWebhookBridge.sln` consumed by CI and stryker (Task 4)

- [ ] **Step 1: Move test files to `tests/` directly**

```bash
git mv tests/GitHubWebhookBridge.Tests/GitHubWebhookBridge.Tests.csproj tests/GitHubWebhookBridge.Tests.csproj
git mv tests/GitHubWebhookBridge.Tests/ActionCoverageTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/ActionFactoryTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/DiscussionActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/ForkActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/IssueCommentActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/IssuesActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/MonkeyTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/MuteManagerTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PingActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PublicActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PullRequestActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PullRequestReviewActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PullRequestReviewCommentActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PullRequestReviewThreadActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/PushActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/SignatureValidatorTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/StarActionTests.cs tests/
git mv tests/GitHubWebhookBridge.Tests/WebhookFunctionTests.cs tests/
rmdir tests/GitHubWebhookBridge.Tests
```

- [ ] **Step 2: Update `tests/GitHubWebhookBridge.Tests.csproj`**

Apply all changes at once.

**Update `ProjectReference`** (was `../../GitHubWebhookBridge.csproj`, now `../src/GitHubWebhookBridge.csproj`):

**Remove** duplicated properties already in `Directory.Build.props`:
```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

The full file after edits:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="coverlet.collector" Version="10.0.1" />
  </ItemGroup>

  <ItemGroup>
    <!-- Xunit 名前空間をグローバル using として追加 -->
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../src/GitHubWebhookBridge.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Update `.editorconfig` test glob**

The section `[tests/**/*.cs]` uses `**/` which matches zero or more path segments — it already covers `tests/Foo.cs`. But to be explicit and unambiguous, update the glob to match direct children of `tests/`:

Find line:
```
[tests/**/*.cs]
```

Replace with:
```
[tests/*.cs]
```

- [ ] **Step 4: Create solution file and add both projects**

```bash
dotnet new sln --name GitHubWebhookBridge --output .
dotnet sln GitHubWebhookBridge.sln add src/GitHubWebhookBridge.csproj
dotnet sln GitHubWebhookBridge.sln add tests/GitHubWebhookBridge.Tests.csproj
```

- [ ] **Step 5: Build and test via solution**

```bash
dotnet build GitHubWebhookBridge.sln -c Release
dotnet test GitHubWebhookBridge.sln -c Release --no-build --settings tests/coverlet.runsettings
```

Expected: build succeeds, all tests pass (168 tests green), coverage ≥ 80%.

- [ ] **Step 6: Commit**

```bash
git add tests/ .editorconfig GitHubWebhookBridge.sln
git commit -m "refactor: テストプロジェクトを tests/ 直下にフラット化、ソリューションファイルを追加"
```

---

## Task 4: Update tooling configs

**Files:**
- Modify: `stryker-config.json`
- Modify: `.github/workflows/azure-functions-deploy.yml`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: `GitHubWebhookBridge.sln` from Task 3

- [ ] **Step 1: Update `stryker-config.json`**

Replace the entire file with:

```json
{
  "stryker-config": {
    "project-info": {
      "name": "github-webhook-bridge",
      "module": "GitHubWebhookBridge"
    },
    "solution": "GitHubWebhookBridge.sln",
    "test-projects": [
      "tests/GitHubWebhookBridge.Tests.csproj"
    ],
    "mutate": [
      "src/Actions/Impl/**/*.cs",
      "src/Actions/BaseAction.cs",
      "src/Actions/ActionFactory.cs",
      "src/Functions/WebhookFunction.cs",
      "src/Utils/SignatureValidator.cs",
      "src/Utils/EmbedHelper.cs",
      "src/Managers/MuteManager.cs"
    ],
    "reporters": ["html", "cleartext", "json"],
    "report-file-name": "stryker-report",
    "threshold": {
      "high": 80,
      "low": 65,
      "break": 60
    },
    "output-path": "StrykerOutput",
    "verbosity": "info"
  }
}
```

- [ ] **Step 2: Update deploy workflow**

In `.github/workflows/azure-functions-deploy.yml`, find the `Publish` step:

```yaml
      - name: Publish
        run: dotnet publish --configuration Release --no-restore --output ${{ env.PUBLISH_OUTPUT_DIR }}
```

Replace with:

```yaml
      - name: Publish
        run: dotnet publish src/GitHubWebhookBridge.csproj --configuration Release --no-restore --output ${{ env.PUBLISH_OUTPUT_DIR }}
```

- [ ] **Step 3: Update CLAUDE.md `func start` command**

In `CLAUDE.md`, find:

```bash
func start                        # run Azure Functions locally
```

Replace with:

```bash
cd src && func start              # run Azure Functions locally
```

- [ ] **Step 4: Verify Stryker config resolves (dry run)**

```bash
dotnet dotnet-stryker --config-file stryker-config.json --dry-run
```

Expected: outputs a list of mutants to be created with no errors. (If `--dry-run` is not supported by the installed version, run `dotnet dotnet-stryker --config-file stryker-config.json --mutate "src/Actions/ActionFactory.cs"` and cancel after it starts to confirm path resolution.)

- [ ] **Step 5: Run full CI simulation**

```bash
dotnet restore GitHubWebhookBridge.sln
dotnet build GitHubWebhookBridge.sln -c Release --no-restore
dotnet test GitHubWebhookBridge.sln -c Release --no-build \
  --settings tests/coverlet.runsettings \
  --collect:"XPlat Code Coverage" --results-directory TestResults
```

Expected: all 168 tests pass, coverage ≥ 80%.

- [ ] **Step 6: Commit**

```bash
git add stryker-config.json .github/workflows/azure-functions-deploy.yml CLAUDE.md
git commit -m "build: stryker・デプロイワークフロー・CLAUDE.md のパスを src/ レイアウトに更新"
```

- [ ] **Step 7: Push**

```bash
git push fork feat/azure-functions-migration
```
