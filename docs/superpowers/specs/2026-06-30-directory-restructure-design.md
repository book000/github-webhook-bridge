# Directory Restructure Design: Flat `src/` + `tests/` Layout

**Date:** 2026-06-30  
**Status:** Approved  
**PR:** #2622 (feat/azure-functions-migration)

---

## Background

The current repository places `GitHubWebhookBridge.csproj` directly at the repo root alongside source code directories (`Actions/`, `Functions/`, etc.) and configuration files, with no `.sln` file. This mixes project files with root-level tooling config and makes the repo root harder to navigate.

The goal is to adopt the `src/` + `tests/` layout endorsed by Microsoft Azure Functions samples, without over-engineering (no Core library split).

---

## Design

### Target Directory Tree

```
github-webhook-bridge/                  ← repo root
├── GitHubWebhookBridge.sln             ← NEW: solution file
├── Directory.Build.props               ← NEW: shared MSBuild properties
├── .editorconfig                       ← unchanged
├── stylecop.json                       ← unchanged
├── stryker-config.json                 ← updated: paths + "solution" key
├── renovate.json                       ← unchanged
├── .config/dotnet-tools.json           ← unchanged
├── .gitignore                          ← updated: src/bin, src/obj entries
│
├── src/                                ← main project lives here directly
│   ├── GitHubWebhookBridge.csproj      ← MOVED from root
│   ├── Program.cs                      ← MOVED
│   ├── host.json                       ← MOVED
│   ├── local.settings.json             ← MOVED (gitignored)
│   ├── packages.lock.json              ← MOVED
│   ├── stylecop.json                   ← kept in src/ (applies to src only)
│   ├── Actions/
│   │   ├── ActionFactory.cs
│   │   ├── BaseAction.cs
│   │   ├── IAction.cs
│   │   ├── IActionFactory.cs
│   │   ├── Impl/
│   │   └── Stubs/
│   ├── Functions/
│   │   └── WebhookFunction.cs
│   ├── Managers/
│   ├── Models/
│   │   ├── Discord/
│   │   └── GitHubWebhooks/
│   │       └── Generated/
│   ├── Services/
│   └── Utils/
│
└── tests/                              ← test project lives here directly
    ├── coverlet.runsettings            ← unchanged
    ├── GitHubWebhookBridge.Tests.csproj ← MOVED up from tests/GitHubWebhookBridge.Tests/
    ├── ActionCoverageTests.cs
    ├── ActionFactoryTests.cs
    └── ...all test .cs files
```

---

## File-Level Changes

### New files

| File | Content |
|---|---|
| `GitHubWebhookBridge.sln` | Registers `src/` and `tests/` projects |
| `Directory.Build.props` | Extracts shared properties from `.csproj` (see below) |

### Moved files

All source files currently at repo root → `src/`.  
All test `.cs` files + `.csproj` currently under `tests/GitHubWebhookBridge.Tests/` → `tests/` directly.

### Updated files

| File | Change |
|---|---|
| `src/GitHubWebhookBridge.csproj` | Remove `<Compile Remove="tests/**" />` (no longer needed); update `stylecop.json` path if referenced |
| `tests/GitHubWebhookBridge.Tests.csproj` | `ProjectReference` → `../src/GitHubWebhookBridge.csproj` |
| `stryker-config.json` | `"solution"` → `"GitHubWebhookBridge.sln"`; `"mutate"` paths → `src/Actions/...` etc. |
| `.gitignore` | Add `src/bin/`, `src/obj/`, `tests/bin/`, `tests/obj/` |
| `.github/workflows/azure-functions-deploy.yml` | `dotnet publish` → add `--project src/GitHubWebhookBridge.csproj` |
| `CLAUDE.md` | Update `func start` command to `cd src && func start` |

### Unchanged files

`dotnet-ci.yml` — `dotnet restore/build/test` resolve all projects via `.sln` automatically; no path changes needed.  
`coverlet.runsettings` — assembly name `[GitHubWebhookBridge]` unchanged.  
`.editorconfig` — path patterns already repo-relative.

---

## `Directory.Build.props` Content

Extract common properties shared between both projects:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

Properties that remain in `src/GitHubWebhookBridge.csproj` only (not shared):
- `AzureFunctionsVersion`, `OutputType`, `RootNamespace`, `AssemblyName`
- `RestorePackagesWithLockFile`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`
- Package references, `InternalsVisibleTo`, `host.json` / `local.settings.json` `<None>` items

Properties that remain in `tests/GitHubWebhookBridge.Tests.csproj` only:
- `IsPackable`, `<Using Include="Xunit" />`

---

## Architecture Decisions

### Why `src/` directly (not `src/GitHubWebhookBridge/`)?

A named subdirectory (`src/GitHubWebhookBridge/`) is useful only when multiple projects share the same `src/` directory (e.g., `src/App/` + `src/App.Core/`). Since this project does not split a Core library, the extra nesting adds no value. `src/` itself is the project.

Same logic applies to `tests/` — there is one test project, so the `GitHubWebhookBridge.Tests/` subdirectory is unnecessary.

### Why add `Directory.Build.props`?

With two projects (`src/` and `tests/`), shared properties (target framework, nullable, implicit usings) would otherwise be duplicated. `Directory.Build.props` at repo root applies to both automatically via MSBuild directory traversal.

### CI/CD impact

`dotnet restore` / `dotnet build` / `dotnet test` without explicit project arguments discover all projects through the `.sln` file. No CI workflow changes are needed except for `dotnet publish` in the deploy workflow, which targets the function app project specifically.

### `func start`

The Azure Functions Core Tools must run from the directory containing `host.json`. After this restructure, that is `src/`:

```bash
# from repo root
cd src && func start
# or
func start --script-root src
```

CLAUDE.md is updated accordingly.

---

## Out of Scope

- Core library extraction (`GitHubWebhookBridge.Core`) — not planned.
- Namespace changes — `RootNamespace` stays `GitHubWebhookBridge`; no code changes needed.
- Moving `stryker-config.json` / `.editorconfig` / `stylecop.json` — root placement is correct.
