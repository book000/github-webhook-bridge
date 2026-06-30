using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Services;

/// <summary>Azure Table Storage のキャッシュエントリを表すクラス。</summary>
public class MessageCacheEntity : ITableEntity
{
    /// <summary>webhookUrl の SHA-256 ハッシュ（32 文字小文字 hex）を取得または設定する。</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>サニタイズ済みメッセージキーを取得または設定する。</summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>キャッシュされた Discord メッセージ ID を取得または設定する。</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>サーバー管理プロパティ（書き込み不可）を取得または設定する。TTL チェックに使用する。</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>楽観的同時実行制御用 ETag を取得または設定する。</summary>
    public ETag ETag { get; set; }
}

/// <summary>
/// Azure Table Storage を使用して Discord メッセージ ID を 5 分間キャッシュするクラス。
/// </summary>
public class MessageCacheService : IMessageCacheService, IDisposable
{
    private const string TableName = "MessageCache";
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    private readonly TableClient _tableClient;
    private readonly ILogger<MessageCacheService> _logger;
    private volatile bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MessageCacheService(IConfiguration config, ILogger<MessageCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        _logger = logger;
        var connStr = config["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");
        var serviceClient = new TableServiceClient(connStr);
        _tableClient = serviceClient.GetTableClient(TableName);
    }

    /// <summary>
    /// テーブルを非同期で作成する。
    /// TableStorageInitializer (IHostedService) から呼ばれる。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理完了を表す <see cref="Task"/>。</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await _tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<CachedMessage?> GetAsync(Uri webhookUrl, string key)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        var partitionKey = HashWebhookUrl(webhookUrl);
        var rowKey = SanitizeRowKey(key);
        NullableResponse<MessageCacheEntity> response = await _tableClient.GetEntityIfExistsAsync<MessageCacheEntity>(partitionKey, rowKey);

        if (!response.HasValue || response.Value is null)
            return null;

        // TTL チェック — 期限切れはテーブルから削除（404 は並行削除などで無視）
        if (response.Value.Timestamp.HasValue
            && DateTimeOffset.UtcNow - response.Value.Timestamp.Value > _cacheTtl)
        {
            try
            {
                await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // 並行削除などでエントリが既に存在しない場合は無視
            }

            return null;
        }

        return new CachedMessage(response.Value.MessageId);
    }

    /// <inheritdoc/>
    public async Task SetAsync(Uri webhookUrl, string key, string messageId)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        // Timestamp は Azure Table Storage がサーバー側で管理するため設定しない
        var entity = new MessageCacheEntity
        {
            PartitionKey = HashWebhookUrl(webhookUrl),
            RowKey = SanitizeRowKey(key),
            MessageId = messageId,
        };
        try
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex)
        {
            // キャッシュ書き込み失敗は警告に留め、処理を継続する
            // （書き込み失敗でも Discord への通知は完了しているため）
            _logger.LogWarning(ex, "Failed to write message cache for key {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Uri webhookUrl, string key)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        var partitionKey = HashWebhookUrl(webhookUrl);
        var rowKey = SanitizeRowKey(key);
        try
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // エントリが既に存在しない場合は無視
        }
    }

    /// <summary>
    /// Azure Table Storage の RowKey に使用できない文字をエスケープする。
    /// 使用禁止文字: /, \, #, ? および制御文字。
    /// 512 文字で切断する際は %XX エンコード三文字組を分断しないよう調整する。
    /// </summary>
    private static string SanitizeRowKey(string key)
    {
        var escaped = Uri.EscapeDataString(key);
        if (escaped.Length <= 512) return escaped;

        var cut = 512;
        if (escaped[cut - 1] == '%') cut -= 1;
        else if (cut >= 2 && escaped[cut - 2] == '%') cut -= 2;
        return escaped[..cut];
    }

    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Azure Table Storage のパーティションキーは小文字 hex で統一する")]
    private static string HashWebhookUrl(Uri webhookUrl)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(webhookUrl.AbsoluteUri));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>マネージドリソースを解放する。</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _initLock.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// ホスト起動時に Table Storage を非同期で初期化する IHostedService 実装クラス。
/// MessageCacheService をクラス直接で注入してコンストラクタでのブロッキング I/O を回避する。
/// </summary>
public class TableStorageInitializer(MessageCacheService service) : IHostedService
{
    private readonly MessageCacheService _service = service;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
        => _service.InitializeAsync(cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
