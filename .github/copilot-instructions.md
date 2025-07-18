# GitHub Copilot 開発指示書

## プロジェクト概要

このプロジェクトは、GitHub の Webhook を受信して Discord にメッセージを送信する Node.js アプリケーションです。GitHub と Discord の連携を強化し、より柔軟な通知システムを提供します。

## 技術スタック

- **言語**: TypeScript (厳密な型設定)
- **Web フレームワーク**: Fastify
- **テスト**: Jest + ts-jest
- **コード品質**: ESLint (@book000/eslint-config) + Prettier
- **パッケージマネージャー**: pnpm
- **ランタイム**: Node.js
- **デプロイ**: Vercel (serverless functions)
- **CI/CD**: GitHub Actions

## コーディング標準

### TypeScript 設定

- 厳密な型チェック（strict: true）
- 未使用の変数・パラメータの検出
- implicit return の禁止
- ESNext + CommonJS モジュール

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

## ファイル構成ルール

```
src/
├── main.ts                 # アプリケーションのエントリーポイント
├── actions/               # 各 GitHub イベントのハンドラー
│   ├── index.ts          # アクション一覧のエクスポート
│   ├── push.ts           # push イベント処理
│   ├── pull-request.ts   # PR イベント処理
│   └── ...               # その他のイベント
├── manager/              # 各種管理機能
│   └── mute.ts          # ミュート機能
├── tests/                # テストファイル（*.test.ts）
├── environments.ts       # 環境変数管理
├── get-action.ts        # アクション取得ロジック
├── embed-colors.ts      # Discord の埋め込み色定義
└── utils.ts             # 共通ユーティリティ
```

## 開発ガイドライン

### GitHub Webhook ハンドラーの作成

1. **新しいイベントハンドラー作成時**:

   ```typescript
   // src/actions/new-event.ts
   import { WebhookEvent } from '@octokit/webhooks-types'
   import { Discord } from '@book000/node-utils'

   export async function handleNewEvent(
     event: WebhookEvent,
     discord: Discord
   ): Promise<void> {
     // イベント処理ロジック
   }
   ```

2. **src/actions/index.ts に追加**:

   ```typescript
   export { handleNewEvent } from './new-event'
   ```

### Discord 連携パターン

- **埋め込みメッセージ**: 構造化された情報表示
- **色分け**: 通知タイプごとに適宜定義し、適切な色を選定する。
- **フィールド構造**: タイトル、説明、フィールド、フッターを適切に使用

### テスト作成ルール

1. **テストファイル命名**: `*.test.ts`
2. **配置場所**: `src/tests/`
3. **テスト構造**:

   ```typescript
   import { describe, expect, test } from '@jest/globals'

   describe('機能名', () => {
     test('具体的なテストケース', () => {
       // テストロジック
       expect(actual).toBe(expected)
     })
   })
   ```

## コミットと PR の規約

### コミットメッセージ（Conventional Commits）

**英語**で Conventional Commits の仕様に従う：

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Type 例**:

- `feat`: 新機能
- `fix`: バグ修正
- `docs`: ドキュメント更新
- `style`: フォーマット変更
- `refactor`: リファクタリング
- `test`: テスト追加・修正
- `chore`: その他のメンテナンス

**例**:

```
feat(webhook): add support for discussion events
fix(discord): resolve embed color issue for PRs
docs: update API documentation
```

### PR タイトルとコミュニケーション

- **PR タイトル**: 英語で Conventional Commits 形式
- **PR 本文**: 日本語
- **レビューコメント**: 日本語
- **Issue/PR 内での会話**: 日本語

## 開発コマンド

```bash
# 依存関係インストール
pnpm install

# 開発サーバー起動
pnpm run dev

# ビルド
pnpm run build

# テスト実行
pnpm run test

# リント実行
pnpm run lint

# フォーマット修正
pnpm run fix
```

## 環境変数

主要な環境変数は `src/environments.ts` で管理：

- `GITHUB_WEBHOOK_SECRET`: GitHub ウェブフックの検証用シークレット
- `DISCORD_WEBHOOK_URL`: Discord 送信先 URL

## コードレビュー観点

1. **型安全性**: TypeScript の型定義が適切か
2. **エラーハンドリング**: 例外処理が適切に実装されているか
3. **テスト**: 新機能に対応するテストが追加されているか
4. **フォーマット**: Prettier と ESLint が通るか
5. **パフォーマンス**: 非効率な処理がないか
6. **セキュリティ**: ウェブフック検証が適切か

## 実装時の注意点

### Webhook セキュリティ

- X-Hub-Signature の検証を必須とする
- utils.ts の `isSignatureValid` 関数を使用

### Discord 連携

- レート制限を考慮した実装
- 埋め込みメッセージの文字数制限に注意
- エラー時の適切なフォールバック

### ミュート機能

- MuteManager を使用してユーザーのミュート状態を管理
- 各イベントハンドラーでミュート状態を確認

## 参考リソース

- [Conventional Commits](https://www.conventionalcommits.org/)
- [GitHub Webhooks Documentation](https://docs.github.com/en/developers/webhooks-and-events/webhooks)
- [Discord Webhook Guide](https://discord.com/developers/docs/resources/webhook)
- [Fastify Documentation](https://www.fastify.io/)

---

このドキュメントは、プロジェクトの成長に合わせて定期的に更新してください。新しい機能や変更があった場合は、対応するセクションの更新も行ってください。
