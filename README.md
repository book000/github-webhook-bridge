# github-webhook-bridge

An Azure Functions application that receives GitHub Webhooks and forwards them to Discord.

- **Endpoints**: `POST /` (webhook receiver), `GET /` (health check)
- **Stack**: C# / .NET 10 / Azure Functions v4 Isolated / Azure Table + Blob + Queue Storage
- **Signature verification**: HMAC-SHA256 (`x-hub-signature-256`, timing-safe comparison)

## Architecture

Receiving a webhook and delivering it to Discord are decoupled, so a Discord outage or rate limit never makes GitHub see an error for an event the bridge has already accepted.

1. `POST /` validates the request (size, signature, event, `?url=`, disabled events, mute rules) and writes the event to the Azure Storage queue `webhook-events`. Payloads over 32 KiB are stored in the blob container `webhook-payloads` and the queue message carries a reference. It then returns `202 Accepted`. If the queue write fails it returns `500`, so GitHub can redeliver.
2. A queue-triggered function (`ProcessWebhookEvent`) builds the Discord message and sends it. Only an explicit `?url=` destination is stored in the queue; the default `DISCORD_WEBHOOK_URL` is read from configuration at processing time.
3. When Discord answers `429 Too Many Requests`, the event is re-enqueued with a delay taken from `Retry-After` (clamped to 1 second–15 minutes, plus up to 5 seconds of jitter). An event that is still rate-limited 6 hours after receipt is moved to the poison queue.
4. Any other failure is retried by the Functions queue host (up to 10 attempts, at least 30 seconds apart; a persistently failing event reached the poison queue after about 8 minutes in a local run) and then moved to the poison queue `webhook-events-poison`.

Delivery is at-least-once. Duplicate posts are controlled by the existing 5-minute message cache: a replay of an event whose Discord message already exists edits that message instead of posting a new one. There is no per-delivery dedupe store, to avoid a storage read and write for every event.

The queues, the payload container (with a 7-day deletion rule), and a single-instance scale-out cap (`WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT=1`, so one worker sends to Discord at a time) are provisioned by `infra/main.bicep`; the application does not create them.

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

- `POST /` (webhook receiver): Receives GitHub webhook events, queues them for delivery to Discord, and returns `202 Accepted`.
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

## Operations

### Finding events that were not delivered

Events that cannot be delivered end up in the storage queue `webhook-events-poison` (storage account `stgithubwebhookbridge`). The worker logs an Error `Webhook event dead-lettered` for events it gives up on, and the exception from a failing delivery also appears in Application Insights, so the existing exception alerts fire.

```bash
az storage message peek --queue-name webhook-events-poison --account-name stgithubwebhookbridge --auth-mode login --num-messages 32
```

Each message body is the Base64 text of a JSON envelope. Decode it (`base64 -d`) to read `DeliveryId`, `EventName`, `ReceivedAt`, and whether the payload is inline (`RawJson`) or stored in the blob container (`PayloadBlob`). Payload blobs are deleted 7 days after creation, so replay or discard a poisoned event within that window.

### Replaying a poisoned event

1. Check the cause in Application Insights (search for `Webhook event dead-lettered` and the `DeliveryId`).
2. Add the message body, unchanged, to the queue `webhook-events` (do not decode or re-encode it), for example with Azure Storage Explorer or the portal, and delete it from the poison queue.
3. A replay posts a new Discord message if the original was already delivered more than 5 minutes ago; replay only events whose Discord message is missing.

### Comparing with GitHub deliveries

In the repository (or organization) webhook settings, open *Recent Deliveries* and match `X-GitHub-Delivery` with the queued `DeliveryId`. A delivery that GitHub shows as failed (`5xx`) was not accepted by the bridge; redeliver it from GitHub. A delivery shown as `202` is already in the queue or was delivered, so redelivering it can post a duplicate.

## License

[MIT](LICENSE)
