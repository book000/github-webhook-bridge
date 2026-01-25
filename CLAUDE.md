# Claude Code 作業方針

## 目的

このドキュメントは、Claude Code がこのプロジェクトで作業を行う際の方針とプロジェクト固有ルールを示します。

## 判断記録のルール

すべての重要な判断は、以下の形式で記録する必要があります：

1. **判断内容の要約**: 何を決定したか
2. **検討した代替案**: どのような選択肢があったか
3. **採用しなかった案とその理由**: なぜその選択肢を選ばなかったか
4. **前提条件・仮定・不確実性**: 判断の前提となる条件や仮定
5. **他エージェントによるレビュー可否**: Codex CLI や Gemini CLI のレビューが必要か

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

## 重要ルール

- **会話言語**: 日本語
- **コード内コメント**: 日本語
- **エラーメッセージ**: 英語
- **日本語と英数字の間**: 半角スペースを挿入
- **コミットメッセージ**: Conventional Commits に従う（`<description>` は日本語）
- **ブランチ命名**: Conventional Branch に従う（`<type>` は短縮形）

## 環境のルール

- **Git コミット**: Conventional Commits に従う
  - 形式: `<type>(<scope>): <description>`
  - `<description>` は日本語で記載
  - 例: `feat: Discord メッセージ送信機能を追加`
- **ブランチ命名**: Conventional Branch に従う
  - 形式: `<type>/<description>`
  - `<type>` は短縮形（feat, fix）を使用
  - 例: `feat/add-discord-notification`
- **GitHub リポジトリ調査**: テンポラリディレクトリに git clone して調査
- **Renovate PR**: 既存の Renovate が作成した PR に対して、追加コミットや更新を行わない

## コード改修時のルール

- **日本語と英数字の間**: 半角スペースを挿入する
- **エラーメッセージの絵文字**: 既存のエラーメッセージに絵文字がある場合、全体で統一する
- **TypeScript の skipLibCheck**: 有効にして回避することは禁止
- **docstring**: 関数やインターフェースに JSDoc を日本語で記載・更新する

## 相談ルール

以下の観点で他エージェントに相談することができます：

### Codex CLI (ask-codex)

- 実装コードに対するソースコードレビュー
- 関数設計、モジュール内部の実装方針などの局所的な技術判断
- アーキテクチャ、モジュール間契約、パフォーマンス／セキュリティといった全体影響の判断
- 実装の正当性確認、機械的ミスの検出、既存コードとの整合性確認

### Gemini CLI (ask-gemini)

- SaaS 仕様、言語・ランタイムのバージョン差、料金・制限・クォータといった、最新の適切な情報が必要な外部依存の判断
- 外部一次情報の確認、最新仕様の調査、外部前提条件の検証

### 指摘への対応ルール

他エージェントが指摘・異議を提示した場合、Claude Code は必ず以下のいずれかを行う。**黙殺・無言での不採用は禁止**。

- 指摘を受け入れ、判断を修正する
- 指摘を退け、その理由を明示する

また、以下を必ず実施する：

- 他エージェントの提案を鵜呑みにせず、その根拠や理由を理解する
- 自身の分析結果と他エージェントの意見が異なる場合は、双方の視点を比較検討する
- 最終的な判断は、両者の意見を総合的に評価した上で、自身で下す

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

## アーキテクチャと主要ファイル

### アーキテクチャサマリー

- **Factory パターン**: `getAction()` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `BaseAction<T>` が全 Action の共通機能を提供
  - `sendMessage()` で 5 分間のメッセージキャッシュを提供
  - メッセージ編集機能（同じキーのメッセージ）
- **Manager パターン**:
  - `BaseRecordManager<T, U>` - Key-value ストアマネージャー
  - `BaseSetManager<T>` - セットベースマネージャー
  - ファイルまたは HTTP URL からロード可能
- **Middleware/Pipeline**: Fastify フックで署名検証とミュートデータロードを実行

### 主要ディレクトリ

```
src/
├── main.ts                 # エントリーポイント（Fastify サーバー）
├── get-action.ts           # Action ファクトリー（60+ switch cases）
├── environments.ts         # 環境変数管理（型安全）
├── utils.ts                # ユーティリティ（署名検証、Embed、メンション）
├── embed-colors.ts         # Discord Embed カラー定数
├── actions/                # 59 個の Action ハンドラーファイル
│   ├── index.ts            # BaseAction 抽象クラス
│   ├── pull-request.ts     # PR イベントハンドラー（複雑なロジック）
│   ├── issues.ts           # Issue イベントハンドラー
│   └── ...                 # その他のイベント
├── manager/                # データ管理レイヤー
│   ├── base-record.ts      # 汎用レコードマネージャー（Map-like）
│   ├── base-set.ts         # 汎用セットマネージャー
│   ├── github-user.ts      # GitHub→Discord ユーザーマッピング
│   └── mute.ts             # 通知ミュートルール
└── tests/                  # Jest テストファイル
```

### データフロー

1. Webhook を POST `/` で受信
2. HMAC-SHA256 署名検証（`utils.ts` の `isSignatureValid`）
3. Mute マネージャーでユーザー・イベントをフィルタリング
4. Action ファクトリーで適切なハンドラー作成
5. Discord Embed メッセージを送信
6. 5 分間キャッシュし、同じキーのメッセージは編集

## 実装パターン

### 推奨パターン

- **新しい Action の追加**:
  1. `src/actions/` に新しいファイルを作成
  2. `BaseAction<T>` を継承
  3. `execute()` メソッドを実装
  4. `src/get-action.ts` の switch 文に追加
- **型安全な環境変数**: `GWBEnvironment` クラスを使用
- **Logger 使用**: `@book000/node-utils` の Logger で構造化ログ
- **Discord メッセージ**: `@book000/node-utils` の Discord ラッパーを使用

### 非推奨パターン

- `skipLibCheck` を有効にして型エラーを回避する
- discord.js を直接使用する（`@book000/node-utils` を使用）
- 環境変数を直接 `process.env` から取得する（`GWBEnvironment` を使用）

## テスト

### テスト方針

- **テストフレームワーク**: Jest 30.2.0
- **テストトランスフォーマー**: ts-jest 29.4.6
- **テストパターン**: `**/*.test.ts`
- **配置場所**: `src/tests/`

### 追加テスト条件

- 新機能追加時はテストを追加する
- 既存テストが失敗しないことを確認する
- 以下のフラグで実行:
  - `--runInBand`: 順次実行
  - `--passWithNoTests`: テストなしで許可
  - `--detectOpenHandles`: リーク検出
  - `--forceExit`: 強制終了

## ドキュメント更新ルール

### 更新対象

- `README.md`: プロジェクト概要、使用方法、環境変数
- `docs/`: イベントタイプ毎のドキュメント（自動生成）
- JSDoc コメント: 関数・インターフェースの docstring

### 更新タイミング

- 新機能追加時
- API 変更時
- 環境変数の追加・変更時

## 作業チェックリスト

### 新規改修時

1. プロジェクトについて詳細に探索し理解する
2. 作業を行うブランチが適切であることを確認する（すでに PR を提出しクローズされたブランチでないこと）
3. 最新のリモートブランチに基づいた新規ブランチであることを確認する
4. PR がクローズされ、不要となったブランチは削除されている
5. プロジェクトで指定されたパッケージマネージャー（pnpm）により、依存パッケージをインストールする

### コミット・プッシュする前

1. コミットメッセージが Conventional Commits に従っている（`<description>` は日本語）
2. コミット内容にセンシティブな情報が含まれていない
3. Lint / Format エラーが発生しない（`pnpm lint` が成功する）
4. 動作確認を行い、期待通り動作する

### プルリクエストを作成する前

1. プルリクエストの作成をユーザーから依頼されている
2. コミット内容にセンシティブな情報が含まれていない
3. コンフリクトする恐れが無い

### プルリクエストを作成したあと

以下を必ず実施する。PR 作成後のプッシュ時に毎回実施する。時間がかかる処理が多いため、Task を使って並列実行する。

1. コンフリクトが発生していない
2. PR 本文の内容は、ブランチの現在の状態を、今までのこの PR での更新履歴を含むことなく、最新の状態のみ、漏れなく日本語で記載されている。この PR を見たユーザーが、最終的にどのような変更を含む PR なのかをわかりやすく、細かく記載されている
3. `gh pr checks <PR ID> --watch` で GitHub Actions CI を待ち、その結果がエラーとなっていない。成功している場合でも、ログを確認し、誤って成功扱いになっていない。もし GitHub Actions が動作しない場合は、ローカルで CI と同等のテストを行い、CI が成功することを保証する
4. `request-review-copilot` コマンドが存在する場合、`request-review-copilot https://github.com/$OWNER/$REPO/pull/$PR_NUMBER` で GitHub Copilot へレビューを依頼する。レビュー依頼は自動で行われる場合もあるし、制約により `request-review-copilot` を実行しても GitHub Copilot がレビューしないケースがある
5. 10 分以内に投稿される GitHub Copilot レビューへの対応を行う。対応したら、レビューコメントそれぞれに対して返信を行う。レビュアーに GitHub Copilot がアサインされていない場合はスキップして構わない
6. `/code-review:code-review` によるコードレビューを実施する。コードレビュー内容に対しては、スコアが 50 以上の指摘事項に対して対応する

## リポジトリ固有

### デプロイ環境

- **Vercel Serverless**: `vercel.json` で全リクエストを `/api/serverless.ts` にルーティング
- **Production URL**: https://github-webhook-bridge.vercel.app

### パッケージマネージャー

- **pnpm のみ使用**: preinstall フックで npm/yarn を禁止
- **バージョン**: 10.28.1+

### CI/CD

- `nodejs-ci-pnpm.yml`: メイン CI（book000/templates の再利用テンプレート）
- `check-import.yml`: Import 検証
- `docker.yml`: Docker イメージビルド
- `generate-docs.yml`: ドキュメント自動生成

### セキュリティ

- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
  - タイミングセーフ比較を使用
- **環境変数**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`, `DISCORD_WEBHOOK_URL`
  - オプション: `API_PORT`, `GITHUB_USER_MAP_FILE_PATH`, `MUTES_FILE_PATH` など

### その他の制約

- **59 種類の GitHub Webhook イベントタイプ**: `@octokit/webhooks-types` で型安全
- **DevContainer サポート**: TypeScript/Node 18 ベースイメージ
- **Renovate**: 依存関係を自動更新（base-public config）
- **ブランチ保護**: main/master ブランチは保護される
- **Discord 統合**: `@book000/node-utils` の Discord ラッパーを使用（discord.js を直接使用しない）
