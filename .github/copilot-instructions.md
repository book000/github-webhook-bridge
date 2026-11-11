# Copilot Code Review Instructions

Review checklist for GitHub Copilot. Development guidance lives in `AGENTS.md`; read it first and do not repeat it in review comments. Keep comments concise and specific to the diff.

## Language

Deliverables (code comments, XML docs, commit messages, PR text, documentation) are English. Flag newly introduced non-English prose, except real-world data fixtures such as the payloads under `tests/RealPayloads/`. Runtime log and error messages are English.

## What to flag

- `#pragma warning disable`, `new HttpClient(...)` in `src/`, `Environment.GetEnvironmentVariable` in `src/`, and direct Discord, Table Storage, or Queue Storage calls that bypass `IDiscordClient`, `IMessageCacheService`, or `IWebhookQueue`.
- Hand-written GitHub payload models (only `Models/Discord/` may hold hand-written DTOs), and event handlers that do not extend `BaseAction<TEvent>` with `[GitHubEvent(...)]`.
- Production members made `public` only to enable testing, and new behavior without xUnit tests under `tests/`.
- Public XML docs or `README.md` that no longer match the behavior.
- Security: a signature comparison in `Utils/SignatureValidator.cs` that is no longer constant-time; anything that widens the `?url=` allowlist (`WebhookUrlValidator.IsAllowedWebhookUrl`); logging of secrets, connection strings, Discord webhook URLs, or full request bodies.

## Do NOT flag (intentional)

- The root route regex `Route = "{x:regex(^$)?}"` with `routePrefix = ""`.
- Editing the existing Discord message when the same key was sent within 5 minutes, and deleting the cache entry then sending a new message on edit failure.
- `SuppressNotifications` forced on for every message.
- `ToLowerInvariant` on hex and event names (CA1308 is suppressed deliberately).
- Underscore test method names (CA1707 / IDE1006 are disabled for `tests/**.cs` in `.editorconfig`).
