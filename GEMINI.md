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
  - 59 種類の GitHub Webhook イベントタイプをサポート
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - メッセージキャッシュと編集機能（5 分間）
  - HMAC-SHA256 による Webhook 署名検証
- **技術スタック**:
  - 言語: TypeScript 5.9.3
  - ランタイム: Node.js 24.13.0
  - フレームワーク: Fastify 5.7.1
  - デプロイ先: Vercel（Serverless）
  - パッケージマネージャー: pnpm 10.28.1+

## コーディング規約

- **フォーマット**: Prettier
  - セミコロンなし
  - シングルクォート使用
  - LF 行末
- **命名規則**:
  - クラス: PascalCase
  - 関数・変数: camelCase
  - 定数: UPPER_SNAKE_CASE
- **コメント言語**: 日本語
- **エラーメッセージ言語**: 英語
- **docstring**: 関数・インターフェースに JSDoc を日本語で記載

## 開発コマンド

```bash
# 依存関係のインストール
pnpm install

# 開発サーバー起動
pnpm start          # main.ts を実行
pnpm dev            # watch モードで実行
pnpm vercel         # Vercel ローカル開発環境

# ビルド
pnpm build          # TypeScript を dist/ にコンパイル

# テスト
pnpm test           # Jest でテストを実行

# Lint / Format
pnpm lint           # 全 Linter 実行（prettier + eslint + tsc）
pnpm fix            # 自動修正（prettier + eslint）
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
- TypeScript strict モードを遵守
- `skipLibCheck` での回避は禁止

### 既知の制約

- **pnpm のみ使用**: preinstall フックで npm/yarn を禁止
- **Vercel デプロイ**: `vercel.json` で全リクエストを `/api/serverless.ts` にルーティング
- **59 種類の GitHub Webhook イベントタイプ**: `@octokit/webhooks-types` で型安全
- **Discord 統合**: `@book000/node-utils` の Discord ラッパーを使用（discord.js を直接使用しない）

## リポジトリ固有

### アーキテクチャ

- **Factory パターン**: `getAction()` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `BaseAction<T>` が全 Action の共通機能を提供
- **Manager パターン**: データ管理レイヤー（`BaseRecordManager<T, U>`, `BaseSetManager<T>`）

### データフロー

1. Webhook を POST `/` で受信
2. HMAC-SHA256 署名検証
3. Mute マネージャーでユーザー・イベントをフィルタリング
4. Action ファクトリーで適切なハンドラー作成
5. Discord Embed メッセージを送信
6. 5 分間キャッシュし、同じキーのメッセージは編集

### デプロイ環境

- **Vercel Serverless**: Production URL は https://github-webhook-bridge.vercel.app
- **環境変数**:
  - 必須: `GITHUB_WEBHOOK_SECRET`, `DISCORD_WEBHOOK_URL`
  - オプション: `API_PORT`, `GITHUB_USER_MAP_FILE_PATH`, `MUTES_FILE_PATH` など

### 主要な外部依存

- **@octokit/webhooks-types** 7.6.1: GitHub Webhook 型定義
- **@book000/node-utils** 1.24.34: カスタムユーティリティ（Discord 統合、Logger）
- **@vercel/node** 5.5.26: Vercel Serverless サポート
- **Fastify** 5.7.1: Web フレームワーク

### Gemini CLI の役割

Gemini CLI は、以下のような最新の外部情報が必要な判断において、Claude Code や他のエージェントをサポートします：

- **SaaS 仕様の確認**: GitHub Webhook API、Discord Webhook API の最新仕様
- **バージョン差の調査**: Node.js、TypeScript、依存ライブラリのバージョン差
- **料金・制限・クォータ**: Vercel のプラン制限、Discord のレート制限
- **外部一次情報の確認**: 公式ドキュメント、リリースノート、変更履歴
- **最新仕様の調査**: 新しい API、機能、ベストプラクティス
