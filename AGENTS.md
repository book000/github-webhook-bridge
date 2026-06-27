# AI エージェント向け作業方針

## 目的

このドキュメントは、一般的な AI エージェントがこのプロジェクトで作業を行う際の共通の作業方針を定義します。

## 基本方針

- **会話言語**: 日本語
- **コード内コメント**: 日本語
- **エラーメッセージ**: 英語
- **日本語と英数字の間**: 半角スペースを挿入
- **コミット規約**: Conventional Commits に従う
  - 形式: `<type>(<scope>): <description>`
  - `<description>` は日本語で記載
  - 例: `feat: Discord メッセージ送信機能を追加`
- **ブランチ命名**: Conventional Branch に従う
  - 形式: `<type>/<description>`
  - `<type>` は短縮形（feat, fix）を使用
  - 例: `feat/add-discord-notification`

## 判断記録のルール

すべての重要な判断は、以下の情報を含めて記録する必要があります：

1. **判断内容**: 何を決定したか
2. **代替案**: どのような選択肢があったか
3. **採用理由**: なぜその選択肢を選んだか
4. **前提条件**: 判断の前提となる条件
5. **不確実性**: 不確実な要素や仮定

**重要**: 前提・仮定・不確実性を明示し、仮定を事実のように扱わないこと。

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

## 技術スタック

- **言語**: C# 14
- **ランタイム**: .NET 10
- **フレームワーク**: Azure Functions v4 Isolated Worker
- **デプロイ先**: Azure Functions
- **パッケージマネージャー**: dotnet CLI（NuGet）
- **テストフレームワーク**: xUnit

## 開発手順（概要）

1. **プロジェクト理解**:
   - README.md を読む
   - 主要なファイル構造を把握する
   - 既存のコードパターンを理解する

2. **依存関係インストール**:
   ```bash
   dotnet restore
   ```

3. **変更実装**:
   - 既存のコードスタイルに従う
   - Nullable 参照型を遵守
   - クラス・公開メソッドに XML ドキュメントコメントを日本語で記載

4. **テスト実行**:
   ```bash
   dotnet build    # ビルドエラー確認
   dotnet test     # テスト実行
   ```

## コーディング規約

- **Nullable 参照型**: 有効化済み（`skipLibCheck` 相当の回避禁止）
- **XML ドキュメントコメント**: クラス・公開メソッドに `///` で日本語記載
- **フォーマット**: editorconfig による自動フォーマット（`EnforceCodeStyleInBuild=true`）
- **命名規則**:
  - クラス・メソッド・プロパティ: PascalCase
  - ローカル変数・パラメータ: camelCase
  - プライベートフィールド: `_` プレフィックス + camelCase

## セキュリティ / 機密情報

- **コミット禁止**: API キーや認証情報を Git にコミットしない
- **ログ禁止**: 個人情報や認証情報をログに出力しない
- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
- **環境変数管理**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`、`AzureWebJobsStorage`
  - オプション: `DISCORD_WEBHOOK_URL`、`GITHUB_USER_MAP_FILE_PATH`、`MUTES_FILE_PATH` など

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

- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` が HTTP トリガー
- **エンドポイント**: `POST /GitHubWebhook`

### パッケージマネージャー

- **dotnet CLI を使用**: `dotnet restore`、`dotnet build`、`dotnet test`

### その他の制約

- **59 種類の GitHub Webhook イベントタイプ**: 12 種実装済み、47 種スタブ（HTTP 406）
- **Renovate**: 依存関係を自動更新（base-public config）
- **ブランチ保護**: main/master ブランチは保護される
- **URL 検証**: `?url=` パラメータは `https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックスのみ許可
