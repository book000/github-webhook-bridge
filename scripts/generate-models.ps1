#!/usr/bin/env pwsh
<#
.SYNOPSIS
    @octokit/webhooks-schemas の JSON Schema から C# モデルクラスを自動生成する。

.DESCRIPTION
    GitHub Webhook イベントの型定義が集約された JSON Schema
    (@octokit/webhooks-schemas npm パッケージ) を取得し、
    NSwag (NJsonSchema) を使用して C# record クラスを生成する。

    生成されたファイルは Models/GitHubWebhooks/Generated/ に配置される。

    前提条件:
      - dotnet tool restore 済み（nswag コマンドが利用可能）
      - curl が利用可能（スキーマのダウンロードに使用）
      - python3 が利用可能（スキーマ分割に使用）

.PARAMETER SchemaVersion
    @octokit/webhooks-schemas のバージョン（デフォルト: latest）。
    "latest" を指定すると npm registry から最新バージョンを取得する。

.PARAMETER OutputDir
    生成ファイルの出力ディレクトリ（プロジェクトルートからの相対パス）。

.PARAMETER Namespace
    生成クラスの C# 名前空間。

.PARAMETER TmpDir
    一時ファイルの保存ディレクトリ。

.PARAMETER Clean
    生成済みファイルをクリアしてから再生成する。

.EXAMPLE
    # デフォルト設定で生成
    ./scripts/generate-models.ps1

    # バージョンを固定して生成
    ./scripts/generate-models.ps1 -SchemaVersion 7.6.1

    # クリーンビルド
    ./scripts/generate-models.ps1 -Clean

.NOTES
    生成ソース: @octokit/webhooks-schemas (https://github.com/octokit/webhooks)
    生成ツール: NSwag / NJsonSchema
    生成先: Models/GitHubWebhooks/Generated/
    名前空間: GitHubWebhookBridge.Models.GitHubWebhooks
#>

param(
    [string]$SchemaVersion = "latest",
    [string]$OutputDir     = "src/Models/GitHubWebhooks/Generated",
    [string]$Namespace     = "GitHubWebhookBridge.Models.GitHubWebhooks",
    [string]$TmpDir        = "",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# ── dotnet SDK の PATH 解決 ─────────────────────────────────────────────────
# 一般的なインストール先を補完（bash プロファイルが読まれない環境向け）
foreach ($extra in @("$env:HOME/.dotnet", "$env:HOME/.dotnet/tools")) {
    if ((Test-Path $extra) -and ($env:PATH -notlike "*$extra*")) {
        $env:PATH = "${extra}:$env:PATH"
    }
}

# ── ルートディレクトリの解決 ────────────────────────────────────────────────
$ScriptDir = $PSScriptRoot ? $PSScriptRoot : (Get-Location).Path
$Root      = Split-Path $ScriptDir -Parent
if (-not (Test-Path (Join-Path $Root "src/GitHubWebhookBridge.csproj"))) {
    Write-Error "GitHubWebhookBridge.csproj が見つかりません。リポジトリルートで実行してください。"
    exit 1
}

# ── 一時ディレクトリの設定 ──────────────────────────────────────────────────
if ([string]::IsNullOrEmpty($TmpDir)) {
    $TmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "octokit-webhooks-schemas"
}
$OutDir     = Join-Path $Root $OutputDir
$SchemaFile = Join-Path $TmpDir "schema.json"
$SplitDir   = Join-Path $TmpDir "split"

# ── ヘルパー関数 ────────────────────────────────────────────────────────────
function Write-Step([string]$msg) {
    Write-Host "  → $msg" -ForegroundColor Cyan
}
function Write-Success([string]$msg) {
    Write-Host "  ✓ $msg" -ForegroundColor Green
}
function Write-Warn([string]$msg) {
    Write-Host "  ! $msg" -ForegroundColor Yellow
}

# ── ツール確認 ──────────────────────────────────────────────────────────────
Write-Host "`n[1/5] 前提ツールの確認" -ForegroundColor White

Push-Location $Root
try {
    $null = & dotnet tool restore 2>&1
    $nswag = & dotnet tool run nswag --version 2>&1
    Write-Success "NSwag: $($nswag | Select-Object -First 1)"
} catch {
    Write-Error "dotnet tool restore に失敗しました: $_"
    exit 1
} finally {
    Pop-Location
}

if (-not (Get-Command curl -ErrorAction SilentlyContinue)) {
    Write-Error "curl が見つかりません。インストールしてください。"
    exit 1
}

if (-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
    Write-Error "python3 が見つかりません。インストールしてください。"
    exit 1
}

# ── JSON Schema のダウンロード ───────────────────────────────────────────────
Write-Host "`n[2/5] @octokit/webhooks-schemas JSON Schema のダウンロード" -ForegroundColor White

New-Item -ItemType Directory -Force -Path $TmpDir | Out-Null

# バージョン解決
if ($SchemaVersion -eq "latest") {
    Write-Step "npm registry からバージョンを取得中..."
    $metaJson  = & curl -s "https://registry.npmjs.org/@octokit/webhooks-schemas/latest"
    $meta      = $metaJson | ConvertFrom-Json
    $SchemaVersion = $meta.version
    Write-Success "バージョン: $SchemaVersion"
}

# schema.json のキャッシュ確認
$cacheTag = Join-Path $TmpDir ".version"
$needsDownload = $true
if ((Test-Path $SchemaFile) -and (Test-Path $cacheTag)) {
    $cachedVersion = Get-Content $cacheTag -Raw
    if ($cachedVersion.Trim() -eq $SchemaVersion) {
        $needsDownload = $false
        Write-Success "キャッシュ済み (v$SchemaVersion)"
    }
}

if ($needsDownload) {
    Write-Step "tarball をダウンロード中 (v$SchemaVersion)..."
    $tarball  = "https://registry.npmjs.org/@octokit/webhooks-schemas/-/webhooks-schemas-$SchemaVersion.tgz"
    $tarPath  = Join-Path $TmpDir "package.tgz"
    & curl -sL -o $tarPath $tarball
    if ($LASTEXITCODE -ne 0) { Write-Error "ダウンロードに失敗しました。"; exit 1 }

    Write-Step "schema.json を展開中..."
    # tarball から schema.json のみ抽出
    Push-Location $TmpDir
    & tar -xz -f $tarPath "package/schema.json" 2>&1 | Out-Null
    Pop-Location

    $extractedSchema = Join-Path $TmpDir "package" "schema.json"
    if (-not (Test-Path $extractedSchema)) {
        Write-Error "schema.json の展開に失敗しました。"
        exit 1
    }
    Copy-Item $extractedSchema $SchemaFile -Force
    Set-Content $cacheTag $SchemaVersion -NoNewline
    Write-Success "schema.json を取得しました (v$SchemaVersion)"
}

# ── スキーマをイベント別に分割 ──────────────────────────────────────────────
Write-Host "`n[3/5] イベント別スキーマの抽出" -ForegroundColor White

New-Item -ItemType Directory -Force -Path $SplitDir | Out-Null

$splitScriptFile = Join-Path $ScriptDir "split_schemas.py"
if (-not (Test-Path $splitScriptFile)) {
    Write-Error "split_schemas.py が見つかりません: $splitScriptFile"
    exit 1
}

Write-Step "Python でイベント別スキーマを分割中..."
$splitOutput = & python3 $splitScriptFile $SchemaFile $SplitDir 2>&1
$splitOutput | ForEach-Object { Write-Host "    $_" }
if ($LASTEXITCODE -ne 0) { Write-Error "スキーマ分割に失敗しました。"; exit 1 }

$splitFiles = Get-ChildItem $SplitDir -Filter "*.json" | Sort-Object Name
Write-Success "$($splitFiles.Count) 件のイベントスキーマを生成しました"

# ── C# コード生成 ──────────────────────────────────────────────────────────
Write-Host "`n[4/5] NSwag による C# モデル生成" -ForegroundColor White

# 出力ディレクトリの準備
if ($Clean -and (Test-Path $OutDir)) {
    Write-Step "既存の生成ファイルをクリア中..."
    Remove-Item -Recurse -Force $OutDir
    Write-Success "クリア完了"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$succeeded = 0
$failed    = 0

foreach ($schemaFile in $splitFiles) {
    $className  = $schemaFile.BaseName
    $outputFile = Join-Path $OutDir "$className.g.cs"

    # イベントごとに sub-namespace を付与し、異なるイベント間で同名の共有型が衝突するのを防ぐ。
    # "Events" を namespace セグメントに使うとスキーマ内の Events クラスと衝突するため
    # "Generated.<ClassName>" 形式にする。
    # 例: GitHubWebhookBridge.Models.GitHubWebhooks.Generated.PingEvent
    $eventNamespace = "$Namespace.Generated.$className"

    Push-Location $Root
    $result = & dotnet tool run nswag jsonschema2csclient `
        /Input:"$($schemaFile.FullName)" `
        /Output:"$outputFile" `
        /Namespace:"$eventNamespace" `
        /Name:"$className" `
        /GenerateNativeRecords:true `
        /JsonLibrary:SystemTextJson `
        /JsonLibraryVersion:8.0 `
        /GenerateDataAnnotations:false `
        /GenerateOptionalPropertiesAsNullable:true `
        /GenerateNullableReferenceTypes:true `
        /GenerateDefaultValues:false `
        /RequiredPropertiesMustBeDefined:false `
        2>&1
    $exitCode = $LASTEXITCODE
    Pop-Location

    if ($exitCode -ne 0 -or -not (Test-Path $outputFile)) {
        Write-Warn "生成失敗: $className"
        $failed++
        continue
    }

    # ── ポスト処理: CLAUDE.md 違反の除去 / NSwag バグ修正 ─────────────────
    $content = Get-Content $outputFile -Raw -Encoding UTF8

    # 1. #pragma warning disable を除去（CLAUDE.md: '#pragma warning disable で型エラーを無視する' は禁止）
    $content = $content -replace '\s*#pragma warning disable.*\r?\n', "`n"

    # 2. NSwag のリネームバグ修正: 型の定義が <T>2 に変名されたが参照が <T> のまま残るケースを修正する。
    #    具体的には、<T> という名前のクラス/レコード定義が存在しないのに <T>2 が存在する場合、
    #    プロパティ型の <T> を <T>2 に置換する。
    $definedTypes   = [System.Text.RegularExpressions.Regex]::Matches($content, '(?:class|record)\s+(\w+)') |
                      ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $referencedTypes = [System.Text.RegularExpressions.Regex]::Matches($content, '\bpublic\s+(?:System\.Collections\.Generic\.ICollection<)?(\w+)(?:>)?\s+\w+') |
                       ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    foreach ($refType in $referencedTypes) {
        if ($refType -match '^\d') { continue }                     # 数字始まりはスキップ
        if ($definedTypes -contains $refType) { continue }          # 定義済みなら問題なし
        $renamed = "${refType}2"
        if ($definedTypes -contains $renamed) {
            # 型参照を <T> → <T>2 に置換（クラス名の一部だけ誤置換しないよう単語境界付き）
            $content = $content -replace "\b$([System.Text.RegularExpressions.Regex]::Escape($refType))\b(?!\d)", $renamed
            Write-Warn "  型リネーム修正: $refType → $renamed ($className)"
        }
    }

    # 3. auto-generated コメントのヘッダーを C# 標準の <auto-generated> 形式に更新
    #    （既存の NSwag コメントはそのまま保持し、補足を追加）
    $header = @"
// <auto-generated/>
// Source: @octokit/webhooks-schemas v$SchemaVersion
// Generator: NSwag / NJsonSchema
// Do not edit this file manually. Re-run scripts/generate-models.ps1 to regenerate.

"@
    $content = $header + ($content -replace '(?s)^.*?namespace', 'namespace')

    Set-Content $outputFile $content -Encoding UTF8 -NoNewline

    $succeeded++
    Write-Host "    ✓ $className.g.cs" -ForegroundColor DarkGreen
}

Write-Host ""
Write-Success "生成完了: 成功 $succeeded / 失敗 $failed"

# ── サマリーレポート ────────────────────────────────────────────────────────
Write-Host "`n[5/5] サマリーレポートの生成" -ForegroundColor White

$generatedFiles = Get-ChildItem $OutDir -Filter "*.g.cs" | Sort-Object Name
$reportFile     = Join-Path $OutDir "GENERATED.md"

$report = @"
# 自動生成モデル — @octokit/webhooks-schemas v$SchemaVersion

生成日時: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
生成ツール: NSwag (NJsonSchema)
生成元: https://www.npmjs.com/package/@octokit/webhooks-schemas

> **注意**: このディレクトリのファイルは自動生成です。手動で編集しないでください。
> 再生成するには ``scripts/generate-models.ps1`` を実行してください。

## 生成済みファイル一覧 ($($generatedFiles.Count) 件)

| ファイル | イベント |
|---|---|
"@

foreach ($f in $generatedFiles) {
    $event = $f.BaseName -replace "Event\.g$", "" -replace "([A-Z])", " `$1" -replace "^ ", ""
    $report += "| ``$($f.Name)`` | $event |`n"
}

$report += @"

## 使用方法

生成モデルは ``GitHubWebhookBridge.Models.GitHubWebhooks`` 名前空間に属します。
手動作成モデル（``Models/GitHubWebhooks/`` 直下）と共存しています。

## 再生成コマンド

```powershell
# 最新バージョンで再生成
./scripts/generate-models.ps1

# バージョンを固定して再生成
./scripts/generate-models.ps1 -SchemaVersion $SchemaVersion

# クリーンビルド
./scripts/generate-models.ps1 -Clean
```
"@

Set-Content $reportFile $report -Encoding UTF8
Write-Success "レポート: $OutputDir/GENERATED.md"

$summary = @"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 生成完了
 スキーマバージョン : @octokit/webhooks-schemas v$SchemaVersion
 生成ファイル数     : $($generatedFiles.Count) ファイル
 出力ディレクトリ   : $OutputDir
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"@
Write-Host $summary -ForegroundColor Green
