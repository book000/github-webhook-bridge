using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>
/// 設定ファイルを Blob / HTTPS URL / ローカルファイルから読み込む抽象基底クラス。
/// 優先順位: Blob > HTTPS URL > ローカルファイル
/// </summary>
/// <typeparam name="TData">デシリアライズ対象のデータ型</typeparam>
public abstract class BaseManager<TData>(IConfiguration config, IHttpClientFactory httpClientFactory) : IDisposable
{
    /// <summary>環境変数から設定されるローカルファイルパスを取得する</summary>
    protected abstract string? FilePath { get; }

    /// <summary>設定ファイルの HTTPS URL を取得する（HTTPS のみ許可）</summary>
    protected abstract Uri? FileUrl { get; }

    /// <summary>
    /// Blob のパスを取得する。形式: "container/path/to/file.json"
    /// 最初の '/' より前がコンテナ名、後がブロブ名
    /// </summary>
    protected abstract string? BlobPath { get; }

    /// <summary>ロード済みのデータを取得する。<see cref="EnsureLoadedAsync"/> 呼び出し後に有効になる</summary>
    protected TData Data { get; private set; } = default!;

    private volatile bool _loaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // JSONC（コメント・末尾カンマ付き JSON）をサポートするオプション
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConfiguration _config = config;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>初回呼び出し時のみデータをロードする（二重初期化防止）</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            var json = await LoadJsonAsync();
            Data = Deserialize(json)
                ?? throw new InvalidOperationException($"Failed to deserialize {GetType().Name} data");
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>JSON 文字列をデシリアライズする。各サブクラスで実装する</summary>
    /// <param name="json">デシリアライズ対象の JSON 文字列</param>
    /// <returns>デシリアライズされたデータ。失敗時は <see langword="null"/></returns>
    protected abstract TData? Deserialize(string json);

    /// <summary>ソース未指定時のデフォルトファイルパスを返す。各サブクラスで実装する</summary>
    protected abstract string GetDefaultFilePath();

    private Task<string> LoadJsonAsync()
    {
        if (BlobPath is not null) return LoadFromBlobAsync(BlobPath);
        if (FileUrl is not null) return LoadFromHttpAsync(FileUrl);
        return LoadFromFileAsync(FilePath ?? GetDefaultFilePath());
    }

    private async Task<string> LoadFromBlobAsync(string blobPath)
    {
        // "container/path/to/file.json" 形式をパース
        var slashIndex = blobPath.IndexOf('/');
        if (slashIndex < 0)
        {
            throw new InvalidOperationException(
                $"BlobPath must be 'container/blob', got: {blobPath}");
        }

        var containerName = blobPath[..slashIndex];
        var blobName = blobPath[(slashIndex + 1)..];

        var connStr = _config["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");

        var blobClient = new BlobClient(connStr, containerName, blobName);
        Response<BlobDownloadResult> download = await blobClient.DownloadContentAsync();
        return download.Value.Content.ToString();
    }

    private async Task<string> LoadFromHttpAsync(Uri url)
    {
        // 設定ファイルは HTTPS 経由のみ許可（平文 HTTP は中間者攻撃のリスク）
        if (!string.Equals(url.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Config URL must use HTTPS: {url}");

        HttpClient http = _httpClientFactory.CreateClient("config");
        return await http.GetStringAsync(url);
    }

    private async Task<string> LoadFromFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var defaultContent = GetDefaultContent();
            await File.WriteAllTextAsync(path, defaultContent);
            return defaultContent;
        }

        return await File.ReadAllTextAsync(path);
    }

    /// <summary>ファイルが存在しない場合に書き込むデフォルトの JSON 内容を返す</summary>
    protected virtual string GetDefaultContent() => "[]";

    /// <summary>型パラメータ T を用いて汎用的に JSON をデシリアライズする</summary>
    /// <typeparam name="T">デシリアライズ対象の型</typeparam>
    /// <param name="json">デシリアライズ対象の JSON 文字列</param>
    /// <returns>デシリアライズされたインスタンス。失敗時は <see langword="null"/></returns>
    protected T? DeserializeJson<T>(string json)
        => JsonSerializer.Deserialize<T>(json, _jsonOptions);

    /// <summary>テスト用: サブクラスがデータを直接設定できるようにする</summary>
    internal void SetDataForTest(TData data)
    {
        Data = data;
        _loaded = true;
    }

    /// <summary>マネージドリソースを解放する</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _lock.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
