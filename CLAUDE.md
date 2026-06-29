# Claude Code — Project Rules

GitHub Webhook receiver that forwards events to Discord.  
Stack: C# / .NET 10, Azure Functions v4 Isolated, Azure Table + Blob Storage, xUnit.

---

## Code Rules

- Code comments and XML doc (`///`): Japanese. Error / log messages: English.
- Never use `#pragma warning disable` to silence analyzer/type errors — fix the code.
  Test-only suppressions belong in the `[tests/**/*.cs]` block in `.editorconfig`.
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
func start                        # run Azure Functions locally

# Model regeneration (OpenAPI → C# models)
pwsh scripts/generate-models.ps1  # or: dotnet tool run nswag
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
6. `Services/DiscordClient.cs` sends the formatted Discord Embed.

**Key patterns**

- `BaseAction<TEvent>` — generic abstract base; `abstract Task RunAsync()`.
- `IAction.RunAsync()` — the interface contract.
- `BaseManager<TData>(IConfiguration, IHttpClientFactory) : IDisposable` — load priority: **Blob > HTTPS URL > local file**.
- Managers: `MuteManager` / `IMuteManager`, `GitHubUserMapManager` / `IGitHubUserMapManager`.
- Services: `DiscordClient` / `IDiscordClient`, `MessageCacheService` / `IMessageCacheService`.
- Utils: `SignatureValidator`, `EmbedColors`, `EmbedHelper`.
- Models in `Models/GitHubWebhooks/Generated/` are generated via `scripts/generate-models.ps1` — do not edit by hand.

---

## Adding a New Action

1. Create a file in `Actions/Impl/` (follow an existing file such as `PushAction.cs`).
2. Extend `BaseAction<TYourEventModel>` and override `RunAsync()` (no parameters; payload available via constructor-injected field).
3. Register the event name in `Actions/ActionFactory.cs` (switch expression).
4. Delete the matching stub class from `Actions/Stubs/StubActions.cs`.
5. Add tests under `tests/GitHubWebhookBridge.Tests/`.

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

**SSRF guard**: `?url=` query parameter is restricted to prefixes  
`https://discord.com/api/webhooks/` and `https://discordapp.com/api/webhooks/` (`IsAllowedWebhookUrl`).

---

## Testing

- Framework: xUnit, project at `tests/GitHubWebhookBridge.Tests/`.
- Add tests for any new behaviour; `dotnet test -c Release` must stay green.
- Analyzer/style rules (including CA1707, IDE1006, CA1308 suppressions for tests) live in `.editorconfig`.

---

## Reference

- **12 implemented** + **46 stub** event types (stubs return HTTP 406).
- CI: `.github/workflows/dotnet-ci.yml` (windows-latest, .NET 10.0.x).
- Deploy: `.github/workflows/azure-functions-deploy.yml` (push to `master`, OIDC → `Azure/functions-action`).
- Keep in sync on any change: `README.md` and XML doc comments (`///`) on public classes/methods.
