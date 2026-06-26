using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GitHubWebhookBridge.Services;

/// <summary>Azure Table Storage キャッシュエントリ。</summary>
public class MessageCacheEntity : ITableEntity
{
    public string          PartitionKey { get; set; } = "";  // webhookUrl の SHA-256 (32 hex chars)
    public string          RowKey       { get; set; } = "";  // サニタイズ済みメッセージキー
    public string          MessageId    { get; set; } = "";
    public DateTimeOffset? Timestamp    { get; set; }        // サーバー管理プロパティ（書き込み不可）
    public ETag            ETag         { get; set; }
}

/// <summary>
/// Azure Table Storage を使用した Discord メッセージ ID の 5 分間キャッシュ。
/// </summary>
public class MessageCacheService : IMessageCacheService
{
    private const  string    TableName = "MessageCache";
    private static readonly TimeSpan   CacheTtl  = TimeSpan.FromMinutes(5);

    private readonly    TableClient   _tableClient;
    private volatile    bool          _initialized;
    private readonly    SemaphoreSlim _initLock = new(1, 1);

    public MessageCacheService(IConfiguration config)
    {
        var connStr = config["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is not set");
        var serviceClient = new TableServiceClient(connStr);
        _tableClient = serviceClient.GetTableClient(TableName);
    }

    /// <summary>
    /// テーブルを非同期で作成する。
    /// TableStorageInitializer (IHostedService) から呼ばれる。
    /// </summary>
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

    public async Task<CachedMessage?> GetAsync(string webhookUrl, string key)
    {
        var partitionKey = HashWebhookUrl(webhookUrl);
        var rowKey       = SanitizeRowKey(key);
        var response     = await _tableClient.GetEntityIfExistsAsync<MessageCacheEntity>(partitionKey, rowKey);

        if (!response.HasValue || response.Value is null)
            return null;

        // TTL チェック — 期限切れはテーブルから削除（404 は並行削除などで無視）
        if (response.Value.Timestamp.HasValue
            && DateTimeOffset.UtcNow - response.Value.Timestamp.Value > CacheTtl)
        {
            try
            {
                await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // 並行削除などでエントリが既に存在しない場合は無視
            }
            return null;
        }

        return new CachedMessage(response.Value.MessageId);
    }

    public async Task SetAsync(string webhookUrl, string key, string messageId)
    {
        // Timestamp は Azure Table Storage がサーバー側で管理するため設定しない
        var entity = new MessageCacheEntity
        {
            PartitionKey = HashWebhookUrl(webhookUrl),
            RowKey       = SanitizeRowKey(key),
            MessageId    = messageId,
        };
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>指定キーのキャッシュエントリを削除する。編集失敗時のフォールバック用。</summary>
    public async Task DeleteAsync(string webhookUrl, string key)
    {
        var partitionKey = HashWebhookUrl(webhookUrl);
        var rowKey       = SanitizeRowKey(key);
        try
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
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

        int cut = 512;
        if (escaped[cut - 1] == '%')         cut -= 1;
        else if (cut >= 2 && escaped[cut - 2] == '%') cut -= 2;
        return escaped[..cut];
    }

    private static string HashWebhookUrl(string webhookUrl)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(webhookUrl));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}

/// <summary>
/// ホスト起動時に Table Storage を非同期で初期化する IHostedService。
/// MessageCacheService を具象型で注入してコンストラクタでのブロッキング I/O を回避する。
/// </summary>
public class TableStorageInitializer : IHostedService
{
    private readonly MessageCacheService _service;

    public TableStorageInitializer(MessageCacheService service)
        => _service = service;

    public Task StartAsync(CancellationToken cancellationToken)
        => _service.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
