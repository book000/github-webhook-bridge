# Gemini CLI 向けコンテキストと作業方針

## 目的

このドキュメントは、Gemini CLI がこのプロジェクトで作業を行う際のコンテキストと作業方針を定義します。

## 出力スタイル

- **言語**: 日本語で回答する
- **トーン**: 技術的かつ簡潔に
- **形式**: 構造化された情報を提供する

## 共通ルール

- **会話言語**: 日本語
- **コミット規約**: Conventional Commits に従う
  - 形式: `<type>(<scope>): <description>`
  - `<description>` は日本語で記載
  - 例: `feat: Discord メッセージ送信機能を追加`
- **ブランチ命名**: Conventional Branch に従う
  - 形式: `<type>/<description>`
  - `<type>` は短縮形（feat, fix）を使用
  - 例: `feat/add-discord-notification`
- **日本語と英数字の間**: 半角スペースを入れる

## プロジェクト概要

- **目的**: GitHub の Webhook を受信し、Discord に通知メッセージを送信する
- **主な機能**:
  - 59 種類の GitHub Webhook イベントタイプをサポート（12 種類実装済み、47 種類はスタブ HTTP 406）
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - メッセージキャッシュと編集機能（5 分間、Azure Table Storage 使用）
  - HMAC-SHA256 による Webhook 署名検証
- **技術スタック**:
  - 言語: C# 14
  - ランタイム: .NET 10
  - フレームワーク: Azure Functions v4 Isolated Worker
  - デプロイ先: Azure Functions
  - パッケージマネージャー: dotnet CLI（NuGet）

## コーディング規約

- **フォーマット**: editorconfig による自動フォーマット
- **命名規則**:
  - クラス・メソッド・プロパティ: PascalCase
  - ローカル変数・パラメータ: camelCase
  - プライベートフィールド: `_` プレフィックス + camelCase
- **コメント言語**: 日本語
- **エラーメッセージ言語**: 英語
- **docstring**: クラス・公開メソッドに XML ドキュメントコメント（`///`）を日本語で記載

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

## 注意事項

### セキュリティ / 機密情報

- **コミット禁止**: API キーや認証情報を Git にコミットしない
- **ログ禁止**: 個人情報や認証情報をログに出力しない
- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数

### 既存ルールの優先

- プロジェクトの既存のコードスタイルに従う
- Nullable 参照型を遵守
- HttpClient は DI 経由で `IHttpClientFactory` を使用（直接インスタンス化禁止）

### 既知の制約

- **dotnet CLI を使用**: `dotnet restore`、`dotnet build`、`dotnet test`
- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` が HTTP トリガー
- **59 種類の GitHub Webhook イベントタイプ**: 12 種実装済み、47 種スタブ（HTTP 406）
- **URL 検証**: `?url=` パラメータは `https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックスのみ許可

## リポジトリ固有

### アーキテクチャ

- **Factory パターン**: `ActionFactory` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `BaseAction` が全 Action の共通機能を提供
- **Manager パターン**: `MuteManager`、`GitHubUserMapManager` でデータ管理

### データフロー

1. Webhook を `POST /GitHubWebhook` で受信
2. HMAC-SHA256 署名検証（`SignatureValidator`）
3. `x-github-event` ヘッダーでイベントタイプを判定
4. `ActionFactory` で適切な Action インスタンスを生成
5. `MuteManager` でミュートチェック後、Discord Embed メッセージを送信

### デプロイ環境

- **Azure Functions v4 Isolated**: エンドポイント `POST /GitHubWebhook`
- **環境変数**:
  - 必須: `GITHUB_WEBHOOK_SECRET`、`AzureWebJobsStorage`
  - オプション: `DISCORD_WEBHOOK_URL`、`GITHUB_USER_MAP_FILE_PATH`、`MUTES_FILE_PATH` など

### 主要な外部依存

- **Microsoft.Azure.Functions.Worker** 2.x: Azure Functions Isolated Worker
- **Azure.Data.Tables** 12.x: Azure Table Storage（メッセージキャッシュ）
- **Azure.Storage.Blobs** 12.x: Azure Blob Storage（Manager データロード）
- **DiffPlex** 1.x: テキスト差分計算

### Gemini CLI の役割

Gemini CLI は、以下のような最新の外部情報が必要な判断において、Claude Code や他のエージェントをサポートします：

- **SaaS 仕様の確認**: GitHub Webhook API、Discord Webhook API の最新仕様
- **バージョン差の調査**: .NET、Azure Functions SDK、依存ライブラリのバージョン差
- **料金・制限・クォータ**: Azure Functions のプラン制限、Discord のレート制限
- **外部一次情報の確認**: 公式ドキュメント、リリースノート、変更履歴
- **最新仕様の調査**: 新しい API、機能、ベストプラクティス
