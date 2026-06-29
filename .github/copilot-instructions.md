# GitHub Copilot Instructions

## プロジェクト概要

- 目的: GitHub の Webhook を受信し、Discord に通知メッセージを送信する
- 主な機能:
  - 12 種類の GitHub Webhook イベントタイプを実装済み（その他のイベントは `UnhandledAction` が HTTP 406 を返す）
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - メッセージキャッシュと編集機能（5 分間、Azure Table Storage 使用）
  - HMAC-SHA256 による Webhook 署名検証
- 対象ユーザー: 開発者、GitHub と Discord を連携させたいユーザー

## 共通ルール

- 会話は日本語で行う。
- コミットメッセージは [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) に従う。
  - 形式: `<type>(<scope>): <description>`
  - `<description>` は日本語で記載
  - 例: `feat: Discord メッセージ送信機能を追加`
- ブランチ命名は [Conventional Branch](https://conventional-branch.github.io) に従う。
  - 形式: `<type>/<description>`
  - `<type>` は短縮形（feat, fix）を使用
  - 例: `feat/add-discord-notification`
- 日本語と英数字の間には半角スペースを入れる。

## 技術スタック

- 言語: C# 14
- ランタイム: .NET 10
- フレームワーク: Azure Functions v4 Isolated Worker
- デプロイ先: Azure Functions
- パッケージマネージャー: dotnet CLI（NuGet）
- テストフレームワーク: xUnit

## コーディング規約

### C# 設定

- Nullable 参照型を有効化（`<Nullable>enable</Nullable>`）
- 暗黙的 using を有効化（`<ImplicitUsings>enable</ImplicitUsings>`）
- editorconfig で定義されたコードスタイルに従う（`EnforceCodeStyleInBuild=true`）
- クラス・公開メソッドに XML ドキュメントコメント（`///`）を日本語で記載・更新
- コメントは日本語で記載
- エラーメッセージは英語で記載
- HttpClient は DI 経由で `IHttpClientFactory` を使用（直接インスタンス化禁止）

### 命名規則

- クラス・メソッド・プロパティ: PascalCase
- ローカル変数・パラメータ: camelCase
- プライベートフィールド: `_` プレフィックス + camelCase

## 開発コマンド

```bash
# 依存パッケージを復元
dotnet restore

# ビルド
dotnet build

# テスト
dotnet test

# Azure Functions ローカル起動
func start
```

## テスト方針

- テストフレームワーク: xUnit
- テストプロジェクト: `tests/GitHubWebhookBridge.Tests/`
- 新機能追加時はテストを追加する
- 既存テストが失敗しないことを確認する

## セキュリティ / 機密情報

- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
  - タイミングセーフ比較を使用（`Utils/SignatureValidator.cs`）
- **環境変数**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`、`AzureWebJobsStorage`
  - オプション: `DISCORD_WEBHOOK_URL`、`GITHUB_USER_MAP_FILE_PATH`、`MUTES_FILE_PATH` など
- **コミット禁止**: API キーや認証情報を Git にコミットしない
- **ログ禁止**: 個人情報や認証情報をログに出力しない
- **Discord 連携**:
  - レート制限を考慮した実装
  - 埋め込みメッセージの文字数制限に注意
  - エラー時の適切なフォールバック

## ドキュメント更新

以下のファイルを更新する必要がある場合は、必ず更新すること：

- `README.md`: プロジェクト概要、使用方法、環境変数
- XML ドキュメントコメント: クラスや公開メソッドの docstring

## リポジトリ固有

### アーキテクチャ

**ディレクトリ構成**:

```
./
├── Program.cs                      # Azure Functions エントリーポイント
├── GitHubWebhookBridge.csproj      # プロジェクトファイル
├── host.json                       # Azure Functions ホスト設定
├── Functions/
│   └── WebhookFunction.cs          # HTTP トリガー関数
├── Actions/
│   ├── IAction.cs                  # Action インターフェース
│   ├── IActionFactory.cs           # Factory インターフェース
│   ├── BaseAction.cs               # 抽象基底クラス
│   ├── ActionFactory.cs            # イベント→Action マッピング
│   ├── Impl/                       # 実装済み 12 Action
│   └── UnhandledAction.cs          # 未実装イベントへの HTTP 406 フォールバック
├── Managers/
│   ├── MuteManager.cs              # ミュートルール管理
│   └── GitHubUserMapManager.cs     # ユーザーマッピング管理
├── Models/                         # GitHub Webhook ペイロードモデル
├── Services/
│   ├── DiscordClient.cs            # Discord Webhook 送信クライアント
│   └── MessageCacheService.cs      # Azure Table Storage メッセージキャッシュ
├── Utils/
│   ├── SignatureValidator.cs        # HMAC-SHA256 署名検証
│   ├── EmbedColors.cs              # Discord Embed カラー定数
│   └── EmbedHelper.cs              # Embed ビルダーヘルパー
└── tests/
    └── GitHubWebhookBridge.Tests/  # xUnit テストプロジェクト
```

**デザインパターン**:

- **Factory パターン**: `ActionFactory` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `BaseAction` が全 Action の共通機能を提供
- **Manager パターン**: `MuteManager`、`GitHubUserMapManager` でデータ管理

**データフロー**:

1. Webhook を `POST /GitHubWebhook` で受信
2. HMAC-SHA256 署名検証（`SignatureValidator`）
3. `x-github-event` ヘッダーでイベントタイプを判定
4. `ActionFactory` で適切な Action インスタンスを生成
5. `MuteManager` でミュートチェック後、Discord Embed メッセージを送信

### GitHub Webhook ハンドラーの作成

1. **新しい Action の追加**:

   ```csharp
   // Actions/Impl/PushAction.cs （既存実装の例）
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

2. **`[GitHubEvent]` 属性を付与するだけで `ActionFactory` が起動時にリフレクションで自動登録する**（switch 文への手動追加は不要）

### Discord 連携パターン

- **埋め込みメッセージ**: `EmbedHelper` で構造化された情報表示
- **色分け**: `EmbedColors` で通知タイプごとに定義
- **フィールド構造**: タイトル、説明、フィールド、フッターを適切に使用

### プロジェクト固有の制約

- **dotnet CLI を使用**: `dotnet restore`、`dotnet build`、`dotnet test`
- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` が HTTP トリガー
- **エンドポイント**: `POST /GitHubWebhook`
- **GitHub Webhook イベントタイプ**: 12 種実装済み、未実装イベントは `UnhandledAction` が HTTP 406 を返す
- **Renovate**: 依存関係を自動更新（base-public config）
- **CI/CD**:
  - `dotnet-ci.yml`: メイン CI（ビルド・テスト）
  - `azure-functions-deploy.yml`: Azure Functions デプロイ（OIDC）
- **ブランチ保護**: main/master ブランチは保護される
- **URL 検証**: `?url=` パラメータは `https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックスのみ許可

## 参考リソース

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Conventional Branch](https://conventional-branch.github.io)
- [GitHub Webhooks Documentation](https://docs.github.com/en/developers/webhooks-and-events/webhooks)
- [Discord Webhook Guide](https://discord.com/developers/docs/resources/webhook)
- [Azure Functions Documentation](https://learn.microsoft.com/azure/azure-functions/)
