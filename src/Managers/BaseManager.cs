using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>
/// Abstract base class that loads a configuration file from a Blob / HTTPS URL / local file.
/// Priority: Blob > HTTPS URL > local file.
/// </summary>
/// <typeparam name="TData">The data type to deserialize into.</typeparam>
public abstract class BaseManager<TData>(IConfiguration config, IHttpClientFactory httpClientFactory) : IDisposable
{
    /// <summary>Gets the local file path configured from an environment variable.</summary>
    protected abstract string? FilePath { get; }

    /// <summary>Gets the HTTPS URL of the configuration file (HTTPS only).</summary>
    protected abstract Uri? FileUrl { get; }

    /// <summary>
    /// Gets the Blob path. Format: "container/path/to/file.json".
    /// The part before the first '/' is the container name; the part after is the blob name.
    /// </summary>
    protected abstract string? BlobPath { get; }

    /// <summary>Gets the loaded data. Valid after <see cref="EnsureLoadedAsync"/> has been called.</summary>
    protected TData Data { get; private set; } = default!;

    private volatile bool _loaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Options that support JSONC (JSON with comments and trailing commas).
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConfiguration _config = config;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>Loads the data only on the first call (prevents double initialization).</summary>
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

    /// <summary>Deserializes a JSON string. Implemented by each subclass.</summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized data, or <see langword="null"/> on failure.</returns>
    protected abstract TData? Deserialize(string json);

    /// <summary>Returns the default file path used when no source is specified. Implemented by each subclass.</summary>
    protected abstract string GetDefaultFilePath();

    private Task<string> LoadJsonAsync()
    {
        if (BlobPath is not null) return LoadFromBlobAsync(BlobPath);
        if (FileUrl is not null) return LoadFromHttpAsync(FileUrl);
        return LoadFromFileAsync(FilePath ?? GetDefaultFilePath());
    }

    private async Task<string> LoadFromBlobAsync(string blobPath)
    {
        // Parse the "container/path/to/file.json" format.
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
        // Allow the configuration file only over HTTPS (plain HTTP carries a man-in-the-middle risk).
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

    /// <summary>Returns the default JSON content to write when the file does not exist.</summary>
    protected virtual string GetDefaultContent() => "[]";

    /// <summary>Generically deserializes JSON using the type parameter T.</summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized instance, or <see langword="null"/> on failure.</returns>
    protected T? DeserializeJson<T>(string json)
        => JsonSerializer.Deserialize<T>(json, _jsonOptions);

    /// <summary>For testing: allows a subclass to set the data directly.</summary>
    internal void SetDataForTest(TData data)
    {
        Data = data;
        _loaded = true;
    }

    /// <summary>Releases managed resources.</summary>
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
