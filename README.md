# github-webhook-bridge

GitHub Webhook を受信して Discord に転送する Azure Functions アプリケーション。

- **エンドポイント**: `POST /`
- **スタック**: C# / .NET 10 / Azure Functions v4 Isolated / Azure Table + Blob Storage
- **署名検証**: HMAC-SHA256（`x-hub-signature-256`、タイミングセーフ比較）

## セットアップ

### 必要なもの

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- Azure Storage アカウント（ローカル開発は [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) で代替可）
- GitHub Webhook シークレット
- Discord Webhook URL

### 環境変数

`src/local.settings.json` に設定する（Azure 本番環境ではアプリケーション設定に登録）。

| キー | 必須 | 説明 |
|------|:----:|------|
| `GITHUB_WEBHOOK_SECRET` | ✅ | HMAC-SHA256 署名検証シークレット |
| `AzureWebJobsStorage` | ✅ | Azure Storage 接続文字列（ローカルは `UseDevelopmentStorage=true`） |
| `DISCORD_WEBHOOK_URL` | — | デフォルト送信先 Discord Webhook URL（`?url=` 未指定時のフォールバック） |
| `DISABLED_EVENTS` | — | 無効化するイベント名（カンマ区切り）。`?disabled-events=` クエリでも上書き可 |
| `MUTES_FILE_PATH` | — | ミュートルール ローカルファイルパス |
| `MUTES_FILE_URL` | — | ミュートルール HTTPS URL |
| `MUTES_BLOB` | — | ミュートルール Azure Blob URI |
| `GITHUB_USER_MAP_FILE_PATH` | — | GitHub→Discord ユーザーマップ ローカルファイルパス |
| `GITHUB_USER_MAP_FILE_URL` | — | ユーザーマップ HTTPS URL |
| `GITHUB_USER_MAP_BLOB` | — | ユーザーマップ Azure Blob URI |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | Application Insights / Azure Monitor 接続文字列 |

### ローカル起動

```bash
dotnet restore
dotnet build

# テスト実行
dotnet test -c Release

# Azure Functions ローカル起動（src/ ディレクトリで実行）
cd src && func start
```

エンドポイント: `http://localhost:7071/`

## エンドポイント

| 環境 | URL |
|------|-----|
| 本番 | `POST https://<functionapp>.azurewebsites.net/` |
| ローカル | `POST http://localhost:7071/` |

### クエリパラメータ

| パラメータ | 説明 |
|-----------|------|
| `url` | 送信先 Discord Webhook URL（`https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` で始まる URL のみ許可） |
| `disabled-events` | このリクエストでのみ無効化するイベント名（カンマ区切り） |

## 対応イベント

### 実装済み（12 種）

| イベント | 説明 |
|---------|------|
| `ping` | Webhook 登録確認 |
| `push` | コードプッシュ |
| `pull_request` | プルリクエスト操作 |
| `pull_request_review` | プルリクエストレビュー |
| `pull_request_review_comment` | レビューコメント |
| `pull_request_review_thread` | レビュースレッド解決・再オープン |
| `issues` | Issue 操作 |
| `issue_comment` | Issue / PR コメント |
| `discussion` | ディスカッション |
| `fork` | フォーク |
| `public` | リポジトリ公開 |
| `star` | スター |

上記以外のイベントは HTTP 406 を返します。

## ミュートルール

ユーザーごとに通知をフィルタリングできます。JSON 配列で記述し、`MUTES_FILE_PATH` / `MUTES_FILE_URL` / `MUTES_BLOB` のいずれかで指定します（Blob > URL > ローカルファイルの優先順）。

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

| `type` | 動作 |
|--------|------|
| `include` | `events` に列挙したイベント・アクションのみミュート |
| `exclude` | `events` に列挙したイベント・アクション以外をミュート |
| `all` | 全通知をミュート |

`actions` を省略するとそのイベントのすべてのアクションが対象になります。

## GitHub→Discord ユーザーマップ

GitHub ユーザー ID（数値）と Discord ユーザー ID を対応付けることで、通知内でメンションを生成できます。JSON オブジェクトで記述し、`GITHUB_USER_MAP_FILE_PATH` / `GITHUB_USER_MAP_FILE_URL` / `GITHUB_USER_MAP_BLOB` のいずれかで指定します。

```json
{
  "1234567": "987654321098765432"
}
```

## ライセンス

[MIT](LICENSE)
