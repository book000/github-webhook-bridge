# github-webhook-bridge

GitHub の Webhook を受信し、Discord に通知メッセージを送信する Azure Functions アプリケーション。

## 概要

- **エンドポイント**: `POST /`
- **技術スタック**: C#、.NET 10、Azure Functions v4 Isolated、Azure Table Storage
- **署名検証**: HMAC-SHA256（`x-hub-signature-256` ヘッダー）

## セットアップ

### 必要なもの

- .NET 10 SDK
- Azure Functions Core Tools v4
- Azure Storage アカウント（またはローカル開発用 Azurite）
- GitHub Webhook シークレット
- Discord Webhook URL

### 環境変数

| 変数名 | 必須 | 説明 |
|--------|------|------|
| `GITHUB_WEBHOOK_SECRET` | ✅ | HMAC-SHA256 署名検証シークレット |
| `AzureWebJobsStorage` | ✅ | Azure Storage 接続文字列（ローカルは `UseDevelopmentStorage=true`） |
| `DISCORD_WEBHOOK_URL` | — | デフォルト Discord Webhook URL（`?url=` 未指定時のフォールバック） |
| `MUTES_FILE_PATH` | — | ミュート設定ローカルパス |
| `MUTES_BLOB_PATH` | — | ミュート設定 Blob パス（例: `container/path/to/mutes.json`） |
| `MUTES_URL` | — | ミュート設定 HTTPS URL |
| `GITHUB_USER_MAP_FILE_PATH` | — | GitHub→Discord ユーザーマップ ローカルパス |
| `GITHUB_USER_MAP_BLOB_PATH` | — | ユーザーマップ Blob パス |
| `GITHUB_USER_MAP_URL` | — | ユーザーマップ HTTPS URL |
| `DISABLED_EVENTS` | — | 無効化するイベント名（カンマ区切り） |

### ローカル開発

```bash
# 依存パッケージを復元
dotnet restore

# Azurite を起動（別ターミナル）
npx azurite --silent

# ビルド
dotnet build

# テスト
dotnet test

# Azure Functions ローカル起動
func start
```

## エンドポイント

- **本番**: `POST https://<functionapp>.azurewebsites.net/GitHubWebhook`
- **ローカル**: `POST http://localhost:7071/GitHubWebhook`

### クエリパラメータ

| パラメータ | 説明 |
|-----------|------|
| `url` | 送信先 Discord Webhook URL（`https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックス必須） |
| `disabled-events` | このリクエストで無効化するイベント名（カンマ区切り） |

## 対応イベント

### 実装済み（12 種）

| イベント | 説明 |
|---------|------|
| `ping` | Webhook 登録確認 |
| `push` | コードプッシュ |
| `pull_request` | プルリクエスト操作 |
| `pull_request_review` | プルリクエストレビュー |
| `pull_request_review_comment` | レビューコメント |
| `pull_request_review_thread` | レビュースレッド |
| `issues` | Issue 操作 |
| `issue_comment` | Issue コメント |
| `discussion` | ディスカッション |
| `fork` | フォーク |
| `public` | リポジトリ公開 |
| `star` | スター |

### スタブ（47 種）

上記以外の GitHub Webhook イベントは HTTP 406 を返します。

## ライセンス

MIT（[LICENSE](LICENSE)）
