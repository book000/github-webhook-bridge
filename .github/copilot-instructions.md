# GitHub Copilot Instructions

## Project Overview

- Purpose: receive GitHub webhooks and send notification messages to Discord
- Key features:
  - 12 GitHub webhook event types implemented (other events get an HTTP 406 from `UnhandledAction`)
  - Discord embed message formatting
  - User mute feature (include/exclude/all modes)
  - GitHub-to-Discord user mapping
  - Event filtering / disabling
  - Message caching and editing (5-minute window, backed by Azure Table Storage)
  - HMAC-SHA256 webhook signature verification
- Target audience: developers who want to connect GitHub and Discord

## General Rules

- English is mandatory for all project artifacts: code, comments, commit messages, PR titles/bodies, and documentation. Japanese is permitted only where quoting real-world Japanese data verbatim is unavoidable.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).
  - Format: `<type>(<scope>): <description>`
  - `<description>` is written in English
  - Example: `feat: add Discord message sending feature`
- Branch names follow [Conventional Branch](https://conventional-branch.github.io).
  - Format: `<type>/<description>`
  - Use the short form for `<type>` (feat, fix)
  - Example: `feat/add-discord-notification`

## Tech Stack

- Language: C# 14
- Runtime: .NET 10
- Framework: Azure Functions v4 Isolated Worker
- Deployment target: Azure Functions
- Package manager: dotnet CLI (NuGet)
- Test framework: xUnit

## Coding Conventions

### C# settings

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- Follow the code style defined in `.editorconfig` (`EnforceCodeStyleInBuild=true`)
- Write/update XML doc comments (`///`) on classes and public methods, in English
- Comments are written in English
- Error messages are written in English
- Use `IHttpClientFactory` via DI for `HttpClient` (direct instantiation is prohibited)

### Naming conventions

- Classes, methods, properties: PascalCase
- Local variables, parameters: camelCase
- Private fields: `_` prefix + camelCase

## Development Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Test
dotnet test

# Start Azure Functions locally
func start
```

## Testing Policy

- Test framework: xUnit
- Test project: `tests/GitHubWebhookBridge.Tests/`
- Add tests when adding new features
- Verify existing tests still pass

## Security / Sensitive Information

- **Webhook verification**: HMAC-SHA256 signature verification is mandatory
  - Header: `x-hub-signature-256`
  - Secret: `GITHUB_WEBHOOK_SECRET` environment variable
  - Uses a timing-safe comparison (`Utils/SignatureValidator.cs`)
- **Environment variables**: credentials are managed via environment variables
  - Required: `GITHUB_WEBHOOK_SECRET`, `AzureWebJobsStorage`
  - Optional: `DISCORD_WEBHOOK_URL`, `GITHUB_USER_MAP_FILE_PATH`, `MUTES_FILE_PATH`, etc.
- **No committing secrets**: never commit API keys or credentials to Git
- **No logging secrets**: never log personal information or credentials
- **Discord integration**:
  - Implementation accounts for rate limits
  - Be mindful of embed message character limits
  - Provide appropriate fallback behavior on errors

## Documentation Updates

When any of the following need updating, make sure to update them:

- `README.md`: project overview, usage, environment variables
- XML doc comments: docstrings on classes and public methods

## Repository-Specific Notes

### Architecture

**Directory layout**:

```
./
├── Program.cs                      # Azure Functions entry point
├── GitHubWebhookBridge.csproj      # project file
├── host.json                       # Azure Functions host configuration
├── Functions/
│   └── WebhookFunction.cs          # HTTP-triggered function
├── Actions/
│   ├── IAction.cs                  # Action interface
│   ├── IActionFactory.cs           # Factory interface
│   ├── BaseAction.cs               # abstract base class
│   ├── ActionFactory.cs            # event → Action mapping
│   ├── Impl/                       # 12 implemented Actions
│   └── UnhandledAction.cs          # HTTP 406 fallback for unimplemented events
├── Managers/
│   ├── MuteManager.cs              # mute rule management
│   └── GitHubUserMapManager.cs     # user mapping management
├── Models/                         # GitHub webhook payload models
├── Services/
│   ├── DiscordClient.cs            # Discord webhook sending client
│   └── MessageCacheService.cs      # Azure Table Storage message cache
├── Utils/
│   ├── SignatureValidator.cs        # HMAC-SHA256 signature verification
│   ├── EmbedColors.cs              # Discord embed color constants
│   └── EmbedHelper.cs              # embed builder helper
└── tests/
    └── GitHubWebhookBridge.Tests/  # xUnit test project
```

**Design patterns**:

- **Factory pattern**: `ActionFactory` maps webhook events to Action classes
- **Abstract base class pattern**: `BaseAction` provides common functionality for all Actions
- **Manager pattern**: `MuteManager`, `GitHubUserMapManager` manage data

**Data flow**:

1. Webhook received via `POST /GitHubWebhook`
2. HMAC-SHA256 signature verification (`SignatureValidator`)
3. Event type determined from the `x-github-event` header
4. `ActionFactory` instantiates the appropriate Action
5. After a mute check by `MuteManager`, a Discord embed message is sent

### Creating a GitHub Webhook Handler

1. **Adding a new Action**:

   ```csharp
   // Actions/Impl/PushAction.cs (example of an existing implementation)
   namespace GitHubWebhookBridge.Actions.Impl;

   [GitHubEvent(WebhookEventType.Push)]
   public class PushAction(IDiscordClient discord, Uri webhookUrl, string eventName,
       PushEvent @event, IMessageCacheService cache, IGitHubUserMapManager userMap,
       ILogger<PushAction> logger)
       : BaseAction<PushEvent>(discord, webhookUrl, eventName, @event, cache, userMap, logger)
   {
       public override async Task RunAsync() { ... }
   }
   ```

2. **Simply adding the `[GitHubEvent]` attribute is enough for `ActionFactory` to auto-register it via reflection at startup** (no manual addition to a switch statement needed)

### Discord Integration Patterns

- **Embed messages**: `EmbedHelper` produces structured information display
- **Color coding**: `EmbedColors` defines colors per notification type
- **Field structure**: title, description, fields, and footer are used appropriately

### Project-Specific Constraints

- **Use the dotnet CLI**: `dotnet restore`, `dotnet build`, `dotnet test`
- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` is the HTTP trigger
- **Endpoint**: `POST /GitHubWebhook`
- **GitHub webhook event types**: 12 implemented; unimplemented events get an HTTP 406 from `UnhandledAction`
- **Renovate**: dependencies are updated automatically (base-public config)
- **CI/CD**:
  - `dotnet-ci.yml`: main CI (build & test)
  - `azure-functions-deploy.yml`: Azure Functions deployment (OIDC)
- **Branch protection**: the main/master branch is protected
- **URL validation**: the `?url=` parameter only allows the `https://discord.com/api/webhooks/` or `https://discordapp.com/api/webhooks/` prefixes

## Reference Resources

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Conventional Branch](https://conventional-branch.github.io)
- [GitHub Webhooks Documentation](https://docs.github.com/en/developers/webhooks-and-events/webhooks)
- [Discord Webhook Guide](https://discord.com/developers/docs/resources/webhook)
- [Azure Functions Documentation](https://learn.microsoft.com/azure/azure-functions/)
