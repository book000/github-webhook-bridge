# Real GitHub Webhook Payloads

This directory vendors **real GitHub webhook example payloads** — one per implemented
event type (12 total) — sourced from
[octokit/webhooks `payload-examples/api.github.com`](https://github.com/octokit/webhooks/tree/main/payload-examples/api.github.com).
These are the same machine-readable examples GitHub's own webhook documentation pages are
generated from, so they reflect the actual field names GitHub sends on the wire.

## Why this exists

`tests/TestFixtures.cs` builds JSON by hand from Octokit.Webhooks' own type definitions
(`[JsonPropertyName]`). If Octokit's mapping is wrong from the start (as happened with
`PullRequestReviewThreadEvent.Review`, which never matched the real `thread` field — see
[#2650](https://github.com/book000/github-webhook-bridge/pull/2650)), a hand-written
fixture built from the same wrong assumption cannot catch the bug.

`tests/RealPayloadIntegrationTests.cs` deserializes these vendored payloads — independent
of Octokit's type shape — and runs each `IAction.RunAsync()` against them, asserting that
values pulled from the real JSON (repository, sender) actually make it into the Discord
message. This catches silent mapping drift that a hand-rolled fixture would miss.

## Files

One example per implemented event, named `<event>.<action>.json` (or `<event>.json` for
events without an `action` field: `fork`, `ping`, `public`, `push`):

| File | GitHub event | Notes |
|---|---|---|
| `discussion.created.json` | `discussion` | `installation` key stripped (see below) |
| `fork.json` | `fork` | |
| `issue_comment.created.json` | `issue_comment` | |
| `issues.opened.json` | `issues` | |
| `ping.json` | `ping` | |
| `public.json` | `public` | |
| `pull_request.opened.json` | `pull_request` | `installation` key stripped |
| `pull_request_review.submitted.json` | `pull_request_review` | `installation` key stripped |
| `pull_request_review_comment.created.json` | `pull_request_review_comment` | `installation` key stripped |
| `pull_request_review_thread.resolved.json` | `pull_request_review_thread` | `installation` key stripped; uses `thread`, not `review` |
| `push.json` | `push` | |
| `star.created.json` | `star` | |

Some upstream examples include an `installation` field (GitHub App context). This app is
configured via a plain per-repository/organization webhook secret (`GITHUB_WEBHOOK_SECRET`),
not a GitHub App, so real production payloads never include it — the key was removed from
the vendored copies to match what this app actually receives.

## Updating

Octokit.Webhooks version bumps (Renovate) do not change these files — they are independent
ground truth. Only refresh them if GitHub changes the actual payload shape for one of these
12 events, or when adding support for a new event type:

```bash
gh api "repos/octokit/webhooks/contents/payload-examples/api.github.com/<event>/<action>.payload.json" \
  --jq '.content' | base64 -d | python3 -c "
import json, sys
d = json.load(sys.stdin)
d.pop('installation', None)  # strip GitHub App-only field; this bridge is not a GitHub App
json.dump(d, sys.stdout, indent=2, ensure_ascii=False)
print()
" > tests/RealPayloads/<event>.<action>.json
```

Then update the file list in `tests/RealPayloadIntegrationTests.cs` if the event/action
changed, and run `dotnet test -c Release`.

## Renovate checklist (Octokit.Webhooks version bumps)

`OctokitPayloadSchemaSnapshotTests` / `OctokitWebhooksCompatibilityTests` catch *structural*
changes in Octokit.Webhooks (renamed C# properties, added/removed event types, enum
changes) but cannot catch Octokit shipping a `[JsonPropertyName]` that was wrong on day one
for a property it didn't change. When reviewing a Renovate PR that bumps
`Octokit.Webhooks`:

1. Run `dotnet test -c Release` — `RealPayloadIntegrationTests` re-validates all 12 vendored
   real payloads against the new version; a mapping regression fails loudly here.
2. If the changelog mentions new/renamed properties on any of the 12 implemented event
   types, cross-check the new `[JsonPropertyName]` against the corresponding file in this
   directory (or the GitHub webhook docs) before merging.
3. If a new event type is added upstream, `OctokitWebhooksCompatibilityTests` will fail
   first — decide whether to implement it (add a fixture here) or record it as
   intentionally unimplemented.
