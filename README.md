# github-webhook-bridge

An Azure Functions application that receives GitHub Webhooks and forwards them to Discord.

- **Endpoints**: `POST /` (webhook receiver), `GET /` (health check)
- **Stack**: C# / .NET 10 / Azure Functions v4 Isolated / Azure Table + Blob Storage
- **Signature verification**: HMAC-SHA256 (`x-hub-signature-256`, timing-safe comparison)

## Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- An Azure Storage account (for local development, [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) can be used instead)
- A GitHub webhook secret
- A Discord webhook URL

### Environment variables

Set these in `src/local.settings.json` (register them as application settings in the Azure production environment).

| Key | Required | Description |
|------|:----:|------|
| `GITHUB_WEBHOOK_SECRET` | ✅ | HMAC-SHA256 signature verification secret |
| `AzureWebJobsStorage` | ✅ | Azure Storage connection string (use `UseDevelopmentStorage=true` locally) |
| `DISCORD_WEBHOOK_URL` | — | Default destination Discord webhook URL (fallback when `?url=` is not specified) |
| `DISABLED_EVENTS` | — | Comma-separated event names to disable; can also be overridden via the `?disabled-events=` query parameter |
| `MUTES_FILE_PATH` | — | Local file path for mute rules |
| `MUTES_FILE_URL` | — | HTTPS URL for mute rules |
| `MUTES_BLOB` | — | Azure Blob URI for mute rules |
| `GITHUB_USER_MAP_FILE_PATH` | — | Local file path for the GitHub→Discord user map |
| `GITHUB_USER_MAP_FILE_URL` | — | HTTPS URL for the user map |
| `GITHUB_USER_MAP_BLOB` | — | Azure Blob URI for the user map |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | Application Insights / Azure Monitor connection string |

### Running locally

```bash
dotnet restore
dotnet build

# Run tests
dotnet test -c Release

# Start Azure Functions locally (run from the src/ directory)
cd src && func start
```

Endpoint: `http://localhost:7071/`

## Endpoints

| Environment | URL |
|------|-----|
| Production | `https://<functionapp>.azurewebsites.net/` |
| Local | `http://localhost:7071/` |

- `POST /` (webhook receiver): Receives GitHub webhook events and forwards them to Discord.
- `GET /` (health check): Returns `200 OK` with `{ "message": "book000/github-webhook-bridge is running" }`.

### Query parameters

| Parameter | Description |
|-----------|------|
| `url` | Destination Discord webhook URL (only URLs starting with `https://discord.com/api/webhooks/` or `https://discordapp.com/api/webhooks/` are allowed) |
| `disabled-events` | Event names to disable for this request only (comma-separated) |

## Supported events

### Implemented (12 types)

| Event | Description |
|---------|------|
| `ping` | Webhook registration check |
| `push` | Code push |
| `pull_request` | Pull request operations |
| `pull_request_review` | Pull request review |
| `pull_request_review_comment` | Review comment |
| `pull_request_review_thread` | Review thread resolve/reopen |
| `issues` | Issue operations |
| `issue_comment` | Issue / PR comment |
| `discussion` | Discussion |
| `fork` | Fork |
| `public` | Repository made public |
| `star` | Star |

Any other event returns HTTP 406.

## Mute rules

Notifications can be filtered per user. Rules are defined as a JSON array and specified via one of `MUTES_FILE_PATH` / `MUTES_FILE_URL` / `MUTES_BLOB` (priority order: Blob > URL > local file).

```json
[
  {
    "userId": 123456789,
    "type": "include",
    "events": [
      { "eventName": "pull_request", "actions": ["opened", "closed"] }
    ]
  }
]
```

| `type` | Behavior |
|--------|------|
| `include` | Mute only the events/actions listed in `events` |
| `exclude` | Mute everything except the events/actions listed in `events` |
| `all` | Mute all notifications |

If `actions` is omitted, all actions of that event are targeted.

## GitHub→Discord user map

Mapping a GitHub user ID (numeric) to a Discord user ID lets notifications generate mentions. Defined as a JSON object and specified via one of `GITHUB_USER_MAP_FILE_PATH` / `GITHUB_USER_MAP_FILE_URL` / `GITHUB_USER_MAP_BLOB`.

```json
{
  "1234567": "987654321098765432"
}
```

## License

[MIT](LICENSE)
