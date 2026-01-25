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
  - 59 種類の GitHub Webhook イベントタイプをサポート
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - メッセージキャッシュと編集機能（5 分間）
  - HMAC-SHA256 による Webhook 署名検証

## 技術スタック

- **言語**: TypeScript 5.9.3
- **ランタイム**: Node.js 24.13.0
- **フレームワーク**: Fastify 5.7.1
- **デプロイ先**: Vercel（Serverless）
- **パッケージマネージャー**: pnpm 10.28.1+（必須）
- **テストフレームワーク**: Jest 30.2.0

## 開発手順（概要）

1. **プロジェクト理解**:
   - README.md を読む
   - 主要なファイル構造を把握する
   - 既存のコードパターンを理解する

2. **依存関係インストール**:
   ```bash
   pnpm install
   ```

3. **変更実装**:
   - 既存のコードスタイルに従う
   - TypeScript strict モードを遵守
   - 関数・インターフェースに JSDoc を日本語で記載

4. **テストと Lint/Format 実行**:
   ```bash
   pnpm test           # テスト実行
   pnpm lint           # Lint チェック
   pnpm fix            # 自動修正
   ```

## コーディング規約

- **TypeScript strict モード**: すべての strict フラグを有効化
  - `skipLibCheck` での回避は禁止
- **関数・インターフェース**: docstring（JSDoc）を日本語で記載・更新
- **フォーマット**: Prettier による自動フォーマット
  - セミコロンなし
  - シングルクォート使用
  - LF 行末
- **命名規則**:
  - クラス: PascalCase
  - 関数・変数: camelCase
  - 定数: UPPER_SNAKE_CASE

## セキュリティ / 機密情報

- **コミット禁止**: API キーや認証情報を Git にコミットしない
- **ログ禁止**: 個人情報や認証情報をログに出力しない
- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
- **環境変数管理**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`, `DISCORD_WEBHOOK_URL`
  - オプション: `API_PORT`, `GITHUB_USER_MAP_FILE_PATH`, `MUTES_FILE_PATH` など

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

- **Vercel Serverless**: `vercel.json` で全リクエストを `/api/serverless.ts` にルーティング
- **Production URL**: https://github-webhook-bridge.vercel.app

### パッケージマネージャー

- **pnpm のみ使用**: preinstall フックで npm/yarn を禁止
- **バージョン**: 10.28.1+

### その他の制約

- **59 種類の GitHub Webhook イベントタイプ**: `@octokit/webhooks-types` で型安全
- **DevContainer サポート**: TypeScript/Node 18 ベースイメージ
- **Renovate**: 依存関係を自動更新（base-public config）
- **ブランチ保護**: main/master ブランチは保護される
- **Discord 統合**: `@book000/node-utils` の Discord ラッパーを使用（discord.js を直接使用しない）
- **Logger**: `@book000/node-utils` の Logger を使用し、構造化エラーログを記録
