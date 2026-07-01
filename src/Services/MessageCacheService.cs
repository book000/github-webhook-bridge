using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Escapes characters that cannot be used in an Azure Table Storage RowKey.
    /// Forbidden characters: /, \, #, ? and control characters.
    /// When truncating at 512 characters, adjusts so as not to split a %XX encoded triplet
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
