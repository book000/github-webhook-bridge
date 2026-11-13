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
4. Any other failure is retried by the Functions queue host (up to 10 attempts, at least 30 seconds apart; a persistently failing event reached the poison queue after about 8 minutes in a local run) and then moved to the poison queue `webhook-events-poison`. The `visibilityTimeout` in `host.json` is this delay before a failed message is retried, not a processing lease: while a delivery is running, the Functions host keeps its message invisible by renewing a lease, so a slow delivery is normally not redelivered concurrently (delivery remains at-least-once). When Discord rejects a request, the exception message includes Discord's response body (truncated to 1000 characters, with the webhook path and token redacted), which names the field and limit at fault.
5. Embed text is truncated to Discord's limits before sending (title 256, description 4096, field name 256, field value 1024, author name 256, footer 2048, at most 25 fields, 6000 characters in total). Truncated text ends with `…`; when the total is exceeded, the description is shortened first and then trailing fields are dropped.

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
| `DISCORD_WEBHOOK_URL` | ✅ unless every request sets `?url=` | Default destination Discord webhook URL, used when `?url=` is not specified (a request without `?url=` fails when this is unset) |
| `DISABLED_EVENTS` | — | Comma-separated event names to disable (case-insensitive, whitespace around names is ignored); can also be overridden via the `?disabled-events=` query parameter |
| `MUTES_FILE_PATH` | — | Local file path for mute rules (default `data/mutes.json`, created as `[]` if missing) |
| `MUTES_FILE_URL` | — | HTTPS URL for mute rules |
| `MUTES_BLOB` | — | Blob path for mute rules, in the form `container/path/to/file.json` (read with `AzureWebJobsStorage`) |
| `GITHUB_USER_MAP_FILE_PATH` | — | Local file path for the GitHub→Discord user map (default `data/github-user-map.json`, created as `{}` if missing) |
| `GITHUB_USER_MAP_FILE_URL` | — | HTTPS URL for the user map |
| `GITHUB_USER_MAP_BLOB` | — | Blob path for the user map, in the form `container/path/to/file.json` (read with `AzureWebJobsStorage`) |
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
| `disabled-events` | Event names to disable for this request only (comma-separated, case-insensitive, whitespace around names is ignored) |

## Supported events

### Implemented (14 types)

| Event | Description |
|---------|------|
| `ping` | Webhook registration check |
| `push` | Code push, including branch/tag creation and deletion and force pushes |
| `pull_request` | Pull request operations |
| `pull_request_review` | Pull request review |
| `pull_request_review_comment` | Review comment |
| `pull_request_review_thread` | Review thread resolve/reopen |
| `issues` | Issue operations |
| `issue_comment` | Issue / PR comment |
| `discussion` | Discussion |
| `discussion_comment` | Discussion comment |
| `fork` | Fork |
| `public` | Repository made public |
| `star` | Star |
| `secret_scanning_alert` | Secret scanning alert (created / publicly leaked / reopened / validated / resolved / revoked; assigned / unassigned are ignored) |

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

If `actions` is omitted, all actions of that event are targeted: for `include` the event is muted for every action, and for `exclude` the event is exempted from muting for every action.

Event names and actions are matched case-insensitively, and whitespace around them is ignored.

If several records share the same `userId`, only the first one is applied and a Warning is logged for each later one.

Malformed records do not fail webhook handling.
They are repaired or dropped when the rules are loaded, and a Warning is logged for each:

- A `null` entry in the rule array, or an event entry without an event name, is dropped.
- A record without `events` behaves like `"events": []`, so `include` mutes nothing and `exclude` mutes everything.
- Blank entries in `actions` are dropped.

## GitHub→Discord user map

Mapping a GitHub user ID (numeric) to a Discord user ID lets notifications generate mentions. Defined as a JSON object and specified via one of `GITHUB_USER_MAP_FILE_PATH` / `GITHUB_USER_MAP_FILE_URL` / `GITHUB_USER_MAP_BLOB`.

```json
{
  "1234567": "987654321098765432"
}
```

### Retrieval failures

The mute rules and the user map are re-read from their source (Blob, URL or file) at most every 5 minutes, checked when the next webhook arrives, so edits take effect within about 5 minutes without restarting the Function App. A webhook that arrives while a refresh is running does not wait and uses the previously loaded data. A file source that was deleted is re-created empty on the next refresh.

If the mute rules or the user map cannot be retrieved (HTTP error such as 429 or 5xx, timeout, Blob or I/O error, malformed JSON or a `null` document), the failure is logged as a Warning (`... could not load its configuration from ...`) and retrieval is not attempted again for 5 minutes. Before the first successful retrieval the service keeps handling webhooks with empty data: no mute rules are applied and no mentions are generated. After a successful retrieval, a failed refresh keeps the previously loaded data.

Misconfiguration still fails fast: a non-HTTPS URL, a Blob path without a container, or a missing `AzureWebJobsStorage` throws instead of degrading.

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
3. A replay posts a new Discord message if the message was last sent or edited more than 5 minutes ago (each successful edit restarts the window); replay only events whose Discord message is missing.

### Comparing with GitHub deliveries

In the repository (or organization) webhook settings, open *Recent Deliveries* and match `X-GitHub-Delivery` with the queued `DeliveryId`. A delivery that GitHub shows as failed (`5xx`) was not accepted by the bridge; redeliver it from GitHub. A delivery shown as `202` is already in the queue or was delivered, so redelivering it can post a duplicate.

## License

[MIT](LICENSE)
