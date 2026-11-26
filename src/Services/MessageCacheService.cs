using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Services;

/// <summary>Class representing a cache entry in Azure Table Storage</summary>
public class MessageCacheEntity : ITableEntity
{
    /// <summary>Gets or sets the SHA-256 hash of webhookUrl (32-character lowercase hex)</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the sanitized message key</summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the cached Discord message ID</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Gets or sets the server-managed property (not writable). Used for the TTL check</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>Gets or sets the ETag for optimistic concurrency control</summary>
    public ETag ETag { get; set; }
}

/// <summary>
/// Class that caches Discord message IDs for 5 minutes using Azure Table Storage
/// </summary>
public class MessageCacheService : IMessageCacheService, IDisposable
{
    private const string TableName = "MessageCache";
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _purgeRetention = TimeSpan.FromHours(1);
    private const int MaxTransactionSize = 100;

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

    internal MessageCacheService(TableClient tableClient, ILogger<MessageCacheService> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    /// <summary>
    /// Creates the table asynchronously.
    /// Called from TableStorageInitializer (IHostedService)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A <see cref="Task"/> representing completion of the operation</returns>
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

        // TTL check — delete expired entries from the table (ignore 404 due to concurrent deletion, etc.)
        if (response.Value.Timestamp.HasValue
            && DateTimeOffset.UtcNow - response.Value.Timestamp.Value > _cacheTtl)
        {
            try
            {
                await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Ignore if the entry no longer exists due to concurrent deletion, etc.
            }

            return null;
        }

        return new CachedMessage(response.Value.MessageId);
    }

    /// <inheritdoc/>
    public async Task SetAsync(Uri webhookUrl, string key, string messageId)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        // Do not set Timestamp because Azure Table Storage manages it on the server side
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
            // Log a cache write failure as a warning only and continue processing
            // (even on write failure, the Discord notification has already completed)
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
            // Ignore if the entry no longer exists
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var filter = BuildExpiredFilter(DateTimeOffset.UtcNow - _purgeRetention);
        var deleted = 0;
        await foreach (Page<TableEntity> page in _tableClient
            .QueryAsync<TableEntity>(filter, select: ["PartitionKey", "RowKey"], cancellationToken: cancellationToken)
            .AsPages()
            .WithCancellation(cancellationToken))
        {
            foreach (IGrouping<string, TableEntity> partition in page.Values.GroupBy(e => e.PartitionKey))
            {
                foreach (TableEntity[] chunk in partition.Chunk(MaxTransactionSize))
                    deleted += await DeleteChunkAsync(chunk, cancellationToken);
            }
        }

        return deleted;
    }

    internal static string BuildExpiredFilter(DateTimeOffset cutoff)
        => $"Timestamp lt datetime'{cutoff.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}'";

    private async Task<int> DeleteChunkAsync(TableEntity[] chunk, CancellationToken cancellationToken)
    {
        IEnumerable<TableTransactionAction> actions = chunk.Select(e => new TableTransactionAction(
            TableTransactionActionType.Delete,
            new TableEntity(e.PartitionKey, e.RowKey) { ETag = ETag.All }));
        try
        {
            await _tableClient.SubmitTransactionAsync(actions, cancellationToken);
            return chunk.Length;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A row vanished (concurrent lazy deletion in GetAsync) and the transaction is atomic, so nothing was deleted: retry row by row.
            var deleted = 0;
            foreach (TableEntity entity in chunk)
            {
                try
                {
                    await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
                    deleted++;
                }
                catch (RequestFailedException inner) when (inner.Status == 404)
                {
                    // Already deleted by someone else
                }
            }

            return deleted;
        }
    }

    /// <summary>
    /// Escapes characters that cannot be used in an Azure Table Storage RowKey.
    /// Forbidden characters: /, \, #, ? and control characters.
    /// A key whose escaped form exceeds 512 characters is shortened to a prefix (never splitting a %XX encoded triplet),
    /// the marker <c>!</c> and 32 hex characters of the SHA-256 of the full key, so distinct long keys do not collide.
    /// <c>Uri.EscapeDataString</c> escapes <c>!</c> to <c>%21</c>, so the marker never appears in an unhashed key,
    /// and every <c>%</c> in the result still starts a complete %XX triplet.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Azure Table Storage row keys are standardized to lowercase hex")]
    internal static string SanitizeRowKey(string key)
    {
        const int MaxLength = 512;
        const string HashMarker = "!";
        const int HashLength = 32;

        var escaped = Uri.EscapeDataString(key);
        if (escaped.Length <= MaxLength) return escaped;

        var cut = MaxLength - HashMarker.Length - HashLength;
        if (escaped[cut - 1] == '%') cut -= 1;
        else if (escaped[cut - 2] == '%') cut -= 2;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..HashLength].ToLowerInvariant();
        return string.Concat(escaped.AsSpan(0, cut), HashMarker, hash);
    }

    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Azure Table Storage partition keys are standardized to lowercase hex")]
    private static string HashWebhookUrl(Uri webhookUrl)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(webhookUrl.AbsoluteUri));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>Releases managed resources</summary>
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
/// IHostedService implementation that asynchronously initializes Table Storage at host startup.
/// Injects MessageCacheService as the concrete class to avoid blocking I/O in the constructor
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
