# Claude Code — Project Rules

GitHub Webhook receiver that forwards events to Discord.  
Stack: C# / .NET 10, Azure Functions v4 Isolated, Azure Table + Blob Storage, xUnit.

---

## Code Rules

- Code comments and XML doc (`///`): Japanese. Error / log messages: English.
- Never use `#pragma warning disable` to silence analyzer/type errors — fix the code.
  (Exception: test-only suppressions go in the `[tests/**/*.cs]` block in `.editorconfig`, never in `#pragma`.)
- Never instantiate `HttpClient` directly — inject `IHttpClientFactory` via DI.
- Send Discord messages via `IDiscordClient` / `DiscordClient`; never call Discord APIs directly.
- Use `IMessageCacheService` / `MessageCacheService` for Azure Table Storage — no raw Azure SDK calls.
- Configuration is read via `IConfiguration` (DI-injected), not `Environment.GetEnvironmentVariable`.

---

## Commands

```bash
dotnet restore                    # restore packages
dotnet build -c Release           # build (mirrors CI)
dotnet test -c Release            # run all tests (mirrors CI)
cd src && func start              # run Azure Functions locally
```

---

## Architecture

**Request flow** — `POST /` (function named `GitHubWebhook`, `Route = ""`, `routePrefix = ""` in `host.json`):

1. `Functions/WebhookFunction.cs` receives the HTTP POST.
2. `Utils/SignatureValidator.cs` verifies HMAC-SHA256 (`x-hub-signature-256`, timing-safe, lowercases hex).
3. `x-github-event` header selects the handler via `Actions/ActionFactory.cs`.
4. `ActionFactory` instantiates the matching `BaseAction<TEvent>` subclass from `Actions/Impl/`
   (payload passed via constructor; override `RunAsync()` — no parameters).
5. `Managers/MuteManager.cs` checks mute rules (include / exclude / all modes).
6. `BaseAction.SendMessageAsync(key, message)` sends via `DiscordClient`; if the same key was sent within the last 5 minutes, it **edits** the existing Discord message instead. On edit failure the cache entry is deleted and a new message is sent. Every message has `SuppressNotifications` forced on.

**Key patterns**

- `BaseAction<TEvent>` — generic abstract base; `abstract Task RunAsync()`.
- `BaseManager<TData>(IConfiguration, IHttpClientFactory) : IDisposable` — load priority: **Blob > HTTPS URL > local file**.
- Managers: `MuteManager` / `IMuteManager`, `GitHubUserMapManager` / `IGitHubUserMapManager`.
- Services: `DiscordClient` / `IDiscordClient`, `MessageCacheService` / `IMessageCacheService`.
- Utils: `SignatureValidator`, `EmbedColors`, `EmbedHelper`.
- GitHub Webhook payload types come from `Octokit.Webhooks` NuGet — do not create hand-written payload models.

---

## Adding a New Action

1. Create a file in `Actions/Impl/` (follow an existing file such as `PushAction.cs`).
2. Extend `BaseAction<TYourEventModel>` and override `RunAsync()` (no parameters; payload available via constructor-injected field).
3. Annotate the class with `[GitHubEvent(WebhookEventType.X)]` — `ActionFactory` auto-registers it via reflection at startup.
4. Add tests under `tests/GitHubWebhookBridge.Tests/`.

---

## Configuration

All keys are read via `IConfiguration`. Required keys must be set in `local.settings.json` / Azure app settings.

| Key | Required | Description |
|---|---|---|
| `GITHUB_WEBHOOK_SECRET` | ✅ | HMAC-SHA256 signing secret |
| `AzureWebJobsStorage` | ✅ | Azure Storage connection string |
| `DISCORD_WEBHOOK_URL` | — | Default Discord webhook URL |
| `DISABLED_EVENTS` | — | Comma-separated event names to disable (also `?disabled-events=` query param) |
| `GITHUB_USER_MAP_FILE_PATH` | — | Local file path for GitHub→Discord user map |
| `GITHUB_USER_MAP_FILE_URL` | — | HTTPS URL for user map |
| `GITHUB_USER_MAP_BLOB` | — | Azure Blob URI for user map |
| `MUTES_FILE_PATH` | — | Local file path for mute rules |
| `MUTES_FILE_URL` | — | HTTPS URL for mute rules |
| `MUTES_BLOB` | — | Azure Blob URI for mute rules |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | Azure Monitor / Application Insights telemetry (OpenTelemetry exporter) |

**SSRF guard**: `?url=` query parameter is restricted to prefixes  
`https://discord.com/api/webhooks/` and `https://discordapp.com/api/webhooks/` (`IsAllowedWebhookUrl`).

---

## Testing

- Framework: xUnit, project at `tests/GitHubWebhookBridge.Tests/`.
- Add tests for any new behaviour; `dotnet test -c Release` must stay green.
- Analyzer/style rules (including CA1707, IDE1006, CA1308 suppressions for tests) live in `.editorconfig`.
- Tests access internal members via `InternalsVisibleTo`; use `SetDataForTest` / `LoadForTest` on managers as test seams — do not make members public to enable testing.

---

## Reference

- **12 implemented** + **46 stub** event types (stubs return HTTP 406).
- CI: `.github/workflows/dotnet-ci.yml` (windows-latest, .NET 10.0.x).
- Deploy: `.github/workflows/azure-functions-deploy.yml` (push to `master`, OIDC → `Azure/functions-action`).
- Keep in sync on any change: `README.md` and XML doc comments (`///`) on public classes/methods.
