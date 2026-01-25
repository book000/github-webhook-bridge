# GitHub Copilot Instructions

## プロジェクト概要

- 目的: GitHub の Webhook を受信し、Discord に通知メッセージを送信する
- 主な機能:
  - 59 種類の GitHub Webhook イベントタイプをサポート
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - メッセージキャッシュと編集機能（5 分間）
  - HMAC-SHA256 による Webhook 署名検証
- 対象ユーザー: 開発者、GitHub と Discord を連携させたいユーザー

## 共通ルール

- 会話は日本語で行う。
- コミットメッセージは [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) に従う。
  - 形式: `<type>(<scope>): <description>`
  - `<description>` は英語で記載
  - 例: `feat: add Discord message sending feature`
- ブランチ命名は [Conventional Branch](https://conventional-branch.github.io) に従う。
  - 形式: `<type>/<description>`
  - `<type>` は短縮形（feat, fix）を使用
  - 例: `feat/add-discord-notification`
- 日本語と英数字の間には半角スペースを入れる。

## 技術スタック

- 言語: TypeScript 5.9.3
- ランタイム: Node.js 24.13.0
- フレームワーク: Fastify 5.7.1
- デプロイ先: Vercel（Serverless）
- パッケージマネージャー: pnpm 10.28.1+（必須）
- ビルドツール: TypeScript Compiler (tsc)
- テストフレームワーク: Jest 30.2.0
- Linter: ESLint 9.39.2 with @book000/eslint-config 1.12.40
- フォーマッター: Prettier 3.8.1

## コーディング規約

### TypeScript 設定

- 厳密な型チェック（strict: true）を有効化
  - noImplicitAny, strictNullChecks, noUnusedLocals などを遵守
  - `skipLibCheck` での回避は禁止
- 関数・インターフェースに docstring（JSDoc）を日本語で記載・更新
- コメントは日本語で記載
- エラーメッセージは英語で記載

### コードフォーマット（Prettier 設定）

```yaml
printWidth: 80
tabWidth: 2
useTabs: false
semi: false # セミコロンなし
singleQuote: true # シングルクォート使用
trailingComma: 'es5' # ES5 形式のトレイリングカンマ
bracketSpacing: true
arrowParens: 'always'
endOfLine: lf
```

### ESLint

- @book000/eslint-config を使用
- standard スタイルベース
- import の順序と Promise の適切な処理

### 命名規則

- クラス: PascalCase
- 関数・変数: camelCase
- 定数: UPPER_SNAKE_CASE

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
pnpm lint:prettier  # フォーマットチェック
pnpm lint:eslint    # ESLint 実行
pnpm lint:tsc       # 型チェックのみ

pnpm fix            # 自動修正（prettier + eslint）
pnpm fix:prettier   # 自動フォーマット
pnpm fix:eslint     # ESLint 自動修正
```

## テスト方針

- テストフレームワーク: Jest 30.2.0
- テストトランスフォーマー: ts-jest 29.4.6
- テストパターン: `**/*.test.ts`
- テストファイル配置場所: `src/tests/`
- 新機能追加時はテストを追加する
- 既存テストが失敗しないことを確認する
- 以下のフラグで実行:
  - `--runInBand`: 順次実行
  - `--passWithNoTests`: テストなしで許可
  - `--detectOpenHandles`: リーク検出
  - `--forceExit`: 強制終了

### テストの書き方

```typescript
import { describe, expect, test } from '@jest/globals'

describe('機能名', () => {
  test('具体的なテストケース', () => {
    // テストロジック
    expect(actual).toBe(expected)
  })
})
```

## セキュリティ / 機密情報

- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
  - タイミングセーフ比較を使用（`utils.ts` の `isSignatureValid` 関数）
- **環境変数**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`, `DISCORD_WEBHOOK_URL`
  - オプション: `API_PORT`, `GITHUB_USER_MAP_FILE_PATH`, `MUTES_FILE_PATH` など
- **コミット禁止**: API キーや認証情報を Git にコミットしない
- **ログ禁止**: 個人情報や認証情報をログに出力しない
- **Discord 連携**:
  - レート制限を考慮した実装
  - 埋め込みメッセージの文字数制限に注意
  - エラー時の適切なフォールバック

## ドキュメント更新

以下のファイルを更新する必要がある場合は、必ず更新すること：

- `README.md`: プロジェクト概要、使用方法、環境変数
- `docs/`: イベントタイプ毎のドキュメント（自動生成）
- JSDoc コメント: 関数・インターフェースの docstring

## リポジトリ固有

### アーキテクチャ

**ディレクトリ構成**:

```
src/
├── main.ts                 # アプリケーションのエントリーポイント
├── actions/               # 各 GitHub イベントのハンドラー
│   ├── index.ts          # BaseAction 抽象クラス
│   ├── pull-request.ts   # PR イベント処理
│   ├── issues.ts         # Issue イベント処理
│   └── ...               # その他のイベント（59 種類）
├── manager/              # 各種管理機能
│   ├── base-record.ts   # 汎用レコードマネージャー
│   ├── base-set.ts      # 汎用セットマネージャー
│   ├── github-user.ts   # GitHub→Discord ユーザーマッピング
│   └── mute.ts          # ミュート機能
├── tests/               # テストファイル（*.test.ts）
├── environments.ts      # 環境変数管理
├── get-action.ts       # アクション取得ロジック（Factory パターン）
├── embed-colors.ts     # Discord の埋め込み色定義
└── utils.ts            # 共通ユーティリティ
```

**デザインパターン**:

- **Factory パターン**: `getAction()` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `BaseAction<T>` が全 Action の共通機能を提供
- **Manager パターン**: `BaseRecordManager<T, U>` と `BaseSetManager<T>` でデータ管理

**データフロー**:

1. Webhook を POST `/` で受信
2. HMAC-SHA256 署名検証
3. Mute マネージャーでユーザー・イベントをフィルタリング
4. Action ファクトリーで適切なハンドラー作成
5. Discord Embed メッセージを送信
6. 5 分間キャッシュし、同じキーのメッセージは編集

### GitHub Webhook ハンドラーの作成

1. **新しいイベントハンドラー作成時**:

   ```typescript
   // src/actions/new-event.ts
   import { BaseAction } from '.'
   import { NewEvent } from '@octokit/webhooks-types'

   export class NewEventAction extends BaseAction<NewEvent> {
     async run(): Promise<void> {
       // イベント処理ロジック
     }
   }
   ```

2. **src/get-action.ts に追加**:

   ```typescript
   case 'new_event':
     return new NewEventAction(discord, eventName, event as NewEvent)
   ```

### Discord 連携パターン

- **埋め込みメッセージ**: 構造化された情報表示
- **色分け**: `embed-colors.ts` で通知タイプごとに定義
- **フィールド構造**: タイトル、説明、フィールド、フッターを適切に使用

### プロジェクト固有の制約

- **pnpm のみ使用**: preinstall フックで npm/yarn を禁止
- **Vercel デプロイ**: `vercel.json` で全リクエストを `/api/serverless.ts` にルーティング
- **59 種類の GitHub Webhook イベントタイプ**: `@octokit/webhooks-types` で型安全
- **DevContainer サポート**: TypeScript/Node 18 ベースイメージ
- **Renovate**: 依存関係を自動更新（base-public config）
- **CI/CD**:
  - `nodejs-ci-pnpm.yml`: メイン CI（book000/templates の再利用テンプレート）
  - `check-import.yml`: Import 検証
  - `docker.yml`: Docker イメージビルド
  - `generate-docs.yml`: ドキュメント自動生成
- **ブランチ保護**: main/master ブランチは保護される
- **Logger**: `@book000/node-utils` の Logger を使用し、構造化エラーログを記録
- **Discord 統合**: `@book000/node-utils` の Discord ラッパーを使用（discord.js を直接使用しない）
- **Mute 機能**: MuteManager を使用してユーザーのミュート状態を管理。Webhook 受信時に `src/main.ts` の `hook()`（`MuteManager.load()` → `isMuted()`）でミュート判定を行い、ミュート対象のイベントは各イベントハンドラーへ渡さない

## 参考リソース

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Conventional Branch](https://conventional-branch.github.io)
- [GitHub Webhooks Documentation](https://docs.github.com/en/developers/webhooks-and-events/webhooks)
- [Discord Webhook Guide](https://discord.com/developers/docs/resources/webhook)
- [Fastify Documentation](https://www.fastify.io/)
