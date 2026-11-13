# AGENTS.md

Guidance for AI coding agents and contributors. `CLAUDE.md` imports this file, and `.github/copilot-instructions.md` adds the review checklist.

## Language

- All deliverables are written in **English**: commit messages, PR/Issue titles and bodies, review comments, code comments and XML docs (`///`), and documentation (`README.md`, `AGENTS.md`, etc.).
- Exceptions (keep as is): quotations, technical identifiers, generated files, external source text, and real-world data fixtures (for example the actual GitHub payloads under `tests/RealPayloads/` and Japanese strings used as test input).
- Runtime messages (logs, errors) are English.
- Do not rewrite Git history to change the language of past commits.

## Overview

A GitHub webhook receiver that forwards events to Discord. C# / .NET 10, Azure Functions v4 Isolated Worker, Azure Table + Blob + Queue Storage, xUnit. See `README.md` for configuration, operations, and the delivery model.

## Commands

```bash
dotnet restore                    # restore packages
dotnet build -c Release           # build (mirrors CI)
dotnet test -c Release            # run all tests (mirrors CI)
cd src && func start              # run Azure Functions locally (needs Core Tools v4 and src/local.settings.json)
```

- CI (`.github/workflows/dotnet-ci.yml`, windows-latest and ubuntu-latest for build/tests, windows-latest for mutation, .NET 10.0.x) runs build, tests with coverage (`tests/coverlet.runsettings`, line coverage must be at least 80%), and on PRs to `master` a Stryker mutation run (`stryker-config.json`).
- Deploy (`.github/workflows/azure-functions-deploy.yml`): push to `master` publishes with OIDC login and `Azure/functions-action`; the infrastructure is `infra/main.bicep`.
- Snapshot tests: after a deliberate Octokit.Webhooks change, refresh with `UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests` and `UPDATE_KNOWN_EVENTS=1 dotnet test --filter OctokitWebhooksCompatibilityTests`, then review the diff. `tests/RealPayloads/README.md` explains the vendored real payloads.

## Architecture

Request flow (`POST /`, function `GitHubWebhook`, `Route = "{x:regex(^$)?}"` with `routePrefix = ""` in `host.json`; `Route = ""` would fall back to the function name, so an empty-match regex binds the literal root):

1. `Functions/WebhookFunction.cs` validates the request: body size, `Utils/SignatureValidator.cs` (HMAC-SHA256, constant-time), `x-github-event`, `?url=` (SSRF allowlist in `Utils/WebhookUrlValidator.cs`), disabled events, and mute rules (`Managers/MuteManager.cs`). Unregistered events return HTTP 406.
2. It stores a `WebhookQueueMessage` through `IWebhookQueue` (payloads over 32 KiB go to the blob container `webhook-payloads`) and returns 202; an enqueue failure returns 500 so GitHub redelivers.
3. The queue-triggered `Functions/ProcessWebhookEventFunction.cs` calls `Functions/WebhookEventProcessor.cs`, which builds the matching `BaseAction<TEvent>` through `ActionFactory` (payload passed via constructor) and runs it.
4. `BaseAction.SendMessageAsync(key, message)` sends through `DiscordClient`; if the same key was sent or edited within the last 5 minutes it edits the existing message instead and each successful edit refreshes the cache entry so the window slides (on edit failure the cache entry is deleted and a new message is sent; if the cache read fails, a warning is logged and a new message is sent). `SuppressNotifications` is forced on for every message.
5. A Discord 429 surfaces as `DiscordRateLimitException` and is re-enqueued honoring `Retry-After`; other failures are retried by the queue host (`maxDequeueCount` 10) and then land in `webhook-events-poison`. The numeric limits live in `WebhookEventProcessor` and must stay in sync with `src/host.json` (guarded by `tests/HostConfigurationTests.cs`).

`GET /` is served by `Functions/RootFunction.cs` (health check) so the default Azure Functions placeholder page never appears on the root route. `ActionRegistryValidator` dry-runs every `[GitHubEvent]` registration at startup.

Key patterns:

- `BaseManager<TData>` (`MuteManager`, `GitHubUserMapManager`): load priority Blob > HTTPS URL > local file. The source is re-read lazily at most every 5 minutes inside `EnsureLoadedAsync` (callers overlapping a refresh get the previous data without waiting). Retrieval failures (HTTP, timeout, Blob, I/O, malformed JSON) log a Warning and are not retried for 5 minutes, continuing with empty data before the first successful load and keeping the previously loaded data afterwards; misconfiguration (non-HTTPS URL, invalid Blob path, missing `AzureWebJobsStorage`) still throws.
- GitHub payload types come from the `Octokit.Webhooks` NuGet package; do not hand-write payload models. Only `Models/Discord/` holds hand-written DTOs (the outbound Discord message shape).
- Infrastructure resources (queues `webhook-events` / `webhook-events-poison`, container `webhook-payloads`, `WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT=1`) are provisioned by `infra/main.bicep`, not created at runtime.

## Adding a new event handler

1. Add a file under `src/Actions/Impl/` (follow `PushAction.cs`).
2. Extend `BaseAction<TEventModel>` and override `RunAsync()` (no parameters; the payload is available through the constructor).
3. Annotate the class with `[GitHubEvent(WebhookEventType.X)]`; `ActionFactory` registers it by reflection at startup.
4. Add tests under `tests/`, add a real payload under `tests/RealPayloads/` (see its README), and update the supported-events table in `README.md`.

## Conventions

- Never use `#pragma warning disable`; fix the code. Test-only relaxations go in the `[tests/**.cs]` block of `.editorconfig`.
- Never instantiate `HttpClient` directly in `src/`; inject `IHttpClientFactory`.
- Send Discord messages through `IDiscordClient`; use `IMessageCacheService` for Table Storage; only `IWebhookQueue` touches Queue Storage and the `webhook-payloads` container.
- Register the `discord` HttpClient only via `AddDiscordHttpClient()` (default request logging is removed because it would log the webhook token). Never log a webhook URL or exception text containing it.
- Read configuration through `IConfiguration`, not `Environment.GetEnvironmentVariable`.
- Tests: xUnit in `tests/` (the project root is `tests/GitHubWebhookBridge.Tests.csproj`); add tests for every behavior change. Internals are reachable through `InternalsVisibleTo`; use the `SetDataForTest` / `LoadForTest` seams instead of making members public.
- Keep `README.md` and public `///` XML docs in sync with behavior changes.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/); branches follow [Conventional Branch](https://conventional-branch.github.io) short form (`feat/...`, `fix/...`). Never commit secrets (`src/local.settings.json` and `data/` are git-ignored).
