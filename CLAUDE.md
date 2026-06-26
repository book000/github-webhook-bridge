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
- **技術スタック**: C#、.NET 10、Azure Functions v4 Isolated、Azure Table Storage
- **主な機能**:
  - 12 種類の GitHub Webhook イベントを完全実装、47 種類はスタブ（HTTP 406）
  - Discord Embed メッセージのフォーマット
  - ユーザーミュート機能（include/exclude/all モード）
  - GitHub から Discord へのユーザーマッピング
  - イベントフィルタリング・無効化機能
  - HMAC-SHA256 による Webhook 署名検証

## 重要ルール

- **会話言語**: 日本語
- **コード内コメント**: 日本語
- **エラーメッセージ**: 英語
- **日本語と英数字の間**: 半角スペースを挿入
- **コミットメッセージ**: Conventional Commits に従う（`<description>` は日本語）
- **ブランチ命名**: Conventional Branch に従う（`<type>` は短縮形）

## 環境のルール

- **Git コミット**: 上記「重要ルール」を参照
- **ブランチ命名**: 上記「重要ルール」を参照
- **GitHub リポジトリ調査**: テンポラリディレクトリに git clone して調査
- **Renovate PR**: 既存の Renovate が作成した PR に対して、追加コミットや更新を行わない

## コード改修時のルール

- **日本語と英数字の間**: 半角スペースを挿入する
- **エラーメッセージの絵文字**: 既存のエラーメッセージに絵文字がある場合、全体で統一する
- **docstring**: クラスや公開メソッドに XML ドキュメントコメント（`///`）を日本語で記載・更新する

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
# 依存パッケージを復元
dotnet restore

# ビルド
dotnet build

# テスト
dotnet test

# Azure Functions ローカル起動
func start
```

## アーキテクチャと主要ファイル

### アーキテクチャサマリー

- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` が HTTP トリガーのエントリーポイント
- **Factory パターン**: `Actions/ActionFactory.cs` が Webhook イベントを Action クラスにマップ
- **Abstract Base Class パターン**: `Actions/BaseAction.cs` が全 Action の共通機能を提供
- **Manager パターン**:
  - `Managers/MuteManager.cs` - ミュートルール管理（include/exclude/all モード）
  - `Managers/GitHubUserMapManager.cs` - GitHub→Discord ユーザーマッピング
  - ファイル・Blob・HTTP URL からロード可能
- **署名検証**: `Utils/SignatureValidator.cs` で HMAC-SHA256 タイミングセーフ検証

### 主要ディレクトリ

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
│   └── Stubs/                      # スタブ 47 Action（HTTP 406）
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

### データフロー

1. Webhook を `POST /GitHubWebhook` で受信
2. HMAC-SHA256 署名検証（`SignatureValidator`）
3. `x-github-event` ヘッダーでイベントタイプを判定
4. `ActionFactory` で適切な Action インスタンスを生成
5. `MuteManager` でミュートチェック後、Discord Embed メッセージを送信

## 実装パターン

### 推奨パターン

- **新しい Action の追加**:
  1. `Actions/Impl/` に新しいファイルを作成する（例: `PushAction.cs` と同じ構成）
  2. `BaseAction` を継承し、`ExecuteAsync(JsonElement payload)` メソッドを実装する
  3. `Actions/ActionFactory.cs` の switch 文に新しいイベントタイプのケースを追加する
  4. `Actions/Stubs/StubActions.cs` にスタブが残っている場合は削除する
- **環境変数**: `Environment.GetEnvironmentVariable()` または DI で注入された `IConfiguration` を使用する
- **Discord メッセージ**: `Services/DiscordClient.cs` を使用する（直接 HTTP クライアントを使用しない）

### 非推奨パターン

- `#pragma warning disable` で型エラーを無視する
- HttpClient を直接インスタンス化する（DI 経由で `IHttpClientFactory` を使用）
- Azure Functions バインディングを使わず独自に Azure SDK を呼ぶ（`MessageCacheService` を使用）

## テスト

### テスト方針

- **テストフレームワーク**: xUnit
- **テストプロジェクト**: `tests/GitHubWebhookBridge.Tests/`
- **実行コマンド**: `dotnet test`

### 追加テスト条件

- 新機能追加時はテストを追加する
- 既存テストが失敗しないことを確認する

## ドキュメント更新ルール

### 更新対象

- `README.md`: プロジェクト概要、使用方法、環境変数
- XML ドキュメントコメント: クラスや公開メソッドの docstring

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
5. `dotnet restore` で依存パッケージをインストールする

### コミット・プッシュする前

1. コミットメッセージが Conventional Commits に従っている（`<description>` は日本語）
2. コミット内容にセンシティブな情報が含まれていない
3. `dotnet build` でビルドエラーが発生しない
4. `dotnet test` でテストが全て PASS する
5. 動作確認を行い、期待通り動作する

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

- **Azure Functions v4 Isolated**: `Functions/WebhookFunction.cs` が HTTP トリガー
- **エンドポイント**: `POST /GitHubWebhook`

### パッケージマネージャー

- **dotnet CLI を使用**: `dotnet restore`、`dotnet build`、`dotnet test`

### CI/CD

- `dotnet-ci.yml`: メイン CI（ビルド・テスト）

### セキュリティ

- **Webhook 検証**: HMAC-SHA256 署名を必須検証
  - ヘッダー: `x-hub-signature-256`
  - シークレット: `GITHUB_WEBHOOK_SECRET` 環境変数
  - タイミングセーフ比較を使用
- **環境変数**: 認証情報は環境変数で管理
  - 必須: `GITHUB_WEBHOOK_SECRET`、`AzureWebJobsStorage`
  - オプション: `DISCORD_WEBHOOK_URL`、`GITHUB_USER_MAP_FILE_PATH`、`MUTES_FILE_PATH` など
- **URL 検証**: `?url=` パラメータは `https://discord.com/api/webhooks/` または `https://discordapp.com/api/webhooks/` プレフィックスのみ許可

### その他の制約

- **59 種類の GitHub Webhook イベントタイプ**: 12 種実装済み、47 種スタブ（HTTP 406）
- **Renovate**: 依存関係を自動更新（base-public config）
- **ブランチ保護**: main/master ブランチは保護される
