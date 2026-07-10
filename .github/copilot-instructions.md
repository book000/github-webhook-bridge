# Copilot Code Review Instructions

Review guidance for this repository. This file is for GitHub Copilot's code
review; day-to-day development guidance lives in `CLAUDE.md` and is not repeated
here. Keep comments concise and specific to the diff.

**Context**: C# / .NET 10 Azure Functions (v4 Isolated Worker) app that receives
GitHub webhooks, verifies their signature, and forwards them to Discord.

## What to flag

### Conventions enforced in this repo

- **English-only artifacts**: code, comments, XML doc (`///`), log/error messages,
  commit messages, and PR titles/bodies must be English. The only allowed exception
  is verbatim real-world Japanese data (e.g. actual GitHub payload text inside test
  fixtures). Flag newly introduced Japanese prose outside that case.
- **No `#pragma warning disable`** to silence analyzer/type warnings — the fix is to
  correct the code. Test-only analyzer relaxations belong in the `[tests/**.cs]`
  block of `.editorconfig`, never as an inline `#pragma`.
- **Dependency-injection boundaries** (flag direct bypasses):
  - `HttpClient` must come from an injected `IHttpClientFactory`, never `new HttpClient(...)`.
  - Discord calls go through `IDiscordClient` / `DiscordClient`, not ad-hoc HTTP.
  - Azure Table Storage access goes through `IMessageCacheService`, not raw Azure SDK calls.
  - Configuration is read via injected `IConfiguration`, not `Environment.GetEnvironmentVariable`.
- **Payload models**: GitHub webhook payload types come from the `Octokit.Webhooks`
  NuGet package. Flag hand-written GitHub payload models. (Hand-written DTOs are
  expected only under `Models/Discord/` for the outbound Discord message shape.)
- **New webhook handlers**: a new event handler should extend `BaseAction<TEvent>`,
  override `RunAsync()` (no parameters), and carry a `[GitHubEvent(WebhookEventType.X)]`
  attribute — `ActionFactory` auto-registers it via reflection. Flag handlers wired up
  by any other mechanism (e.g. manual switch statements).

### Security (high priority)

- Webhook signature verification (`Utils/SignatureValidator.cs`, HMAC-SHA256 over
  `x-hub-signature-256`) must stay **constant-time**. Flag any change to a
  short-circuiting or `==`/`.Equals` string comparison of the signature.
- The `?url=` SSRF guard (`IsAllowedWebhookUrl`) restricts destinations to the
  `https://discord.com/api/webhooks/` and `https://discordapp.com/api/webhooks/`
  prefixes. Flag anything that widens or bypasses this allowlist.
- No secrets in code, logs, or committed config. Flag logging of webhook secrets,
  connection strings, or full request bodies that may embed tokens.

### Tests

- New behaviour needs xUnit tests directly under `tests/` (the `tests/` directory is the test project root, `tests/GitHubWebhookBridge.Tests.csproj`).
- Tests reach internal members via `InternalsVisibleTo` and existing seams
  (`SetDataForTest` / `LoadForTest`). Flag production members being made `public`
  purely to enable testing.

## Do NOT flag (known intentional patterns)

- **Root route regex** `Route = "{x:regex(^$)?}"` with `routePrefix = ""` in
  `host.json` — this deliberately binds the literal root path `/`; it is not a typo.
- **Message editing**: when the same cache key was sent within 5 minutes, the code
  edits the existing Discord message instead of sending a new one. On edit failure it
  deletes the cache entry and sends fresh. This is intended, not a race-condition bug.
- **`SuppressNotifications` forced on** for every Discord message — intentional.
- **`ToLowerInvariant` on hex** in signature handling — intentional (CA1308 is
  relaxed for tests in `.editorconfig`); do not push `ToUpperInvariant`.
- **Underscore-containing test method names** — CA1707 / IDE1006 are intentionally
  disabled for `tests/**` in `.editorconfig`.

## Conventions reference

- Commits: [Conventional Commits](https://www.conventionalcommits.org/) (`<type>(<scope>): <description>`), English description.
- Branches: [Conventional Branch](https://conventional-branch.github.io) short form (`feat`, `fix`, ...).
- Keep `README.md` and public `///` XML docs in sync with behavioural changes.
