#!/usr/bin/env pwsh
<#
.SYNOPSIS
    GitHub Webhook モデルクラスの生成に関するドキュメントスクリプト

.DESCRIPTION
    このスクリプトは、GitHub Webhook イベントの C# モデルクラスが
    どのように作成されたかを記録するものです。

    【生成方法】
    モデルクラスは NJsonSchema による自動生成ではなく、以下の手順で
    手動で作成されました。

    理由:
    - このリポジトリには JSON Schema ファイルが存在しない
    - TypeScript の型定義（node_modules/@octokit/webhooks-types/schema.d.ts）
      をベースに手動で C# クラスを作成した

    【対象ファイル】
    Models/GitHubWebhooks/ ディレクトリ以下に以下のファイルが存在します：
    - Common.cs                           共通モデル型（User, Repository, Commit 等）
    - PingEvent.cs                        ping イベント
    - PushEvent.cs                        push イベント
    - StarEvent.cs                        star イベント
    - ForkEvent.cs                        fork イベント
    - PublicEvent.cs                      public イベント
    - IssuesEvent.cs                      issues イベント
    - IssueCommentEvent.cs                issue_comment イベント
    - PullRequestEvent.cs                 pull_request イベント
    - PullRequestReviewEvent.cs           pull_request_review イベント
    - PullRequestReviewCommentEvent.cs    pull_request_review_comment イベント
    - PullRequestReviewThreadEvent.cs     pull_request_review_thread イベント
    - DiscussionEvent.cs                  discussion イベント

    【スタブアクション】
    上記 12 イベント以外の 47 イベントはスタブとして実装されており、
    System.Text.Json.JsonElement を直接使用します。
    これらのスタブは NotImplementedException をスローします。

    【将来の自動生成について】
    octokit/webhooks リポジトリの JSON Schema から自動生成する場合は、
    以下のコマンドを参考にしてください：

    # スキーマのスパースクローン
    git clone --depth 1 --filter=blob:none --sparse `
        https://github.com/octokit/webhooks.git tmp/octokit-schemas
    cd tmp/octokit-schemas
    git sparse-checkout set payload-schemas/schemas
    cd ../..

    # dotnet ツールのリストア
    dotnet tool restore

    # NSwag を使ったコード生成（将来の実装用）
    # dotnet tool run nswag ...

.NOTES
    作成日: 2026-06-26
    参照: node_modules/@octokit/webhooks-types/schema.d.ts
    名前空間: GitHubWebhookBridge.Models.GitHubWebhooks
#>

param(
    [string]$OutputDir = "Models/GitHubWebhooks",
    [string]$Namespace = "GitHubWebhookBridge.Models.GitHubWebhooks"
)

Write-Host "GitHub Webhook モデルクラス情報" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "出力ディレクトリ: $OutputDir"
Write-Host "名前空間: $Namespace"
Write-Host ""

$modelFiles = Get-ChildItem -Path $OutputDir -Filter "*.cs" -ErrorAction SilentlyContinue
if ($modelFiles) {
    Write-Host "現在のモデルファイル一覧 ($($modelFiles.Count) ファイル):" -ForegroundColor Green
    $modelFiles | ForEach-Object {
        Write-Host "  - $($_.Name)"
    }
} else {
    Write-Host "モデルファイルが見つかりません: $OutputDir" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "注意: このスクリプトはモデルを自動生成しません。" -ForegroundColor Yellow
Write-Host "モデルクラスは TypeScript 型定義から手動で作成されています。" -ForegroundColor Yellow
Write-Host "詳細はスクリプト内のコメントを参照してください。" -ForegroundColor Yellow
