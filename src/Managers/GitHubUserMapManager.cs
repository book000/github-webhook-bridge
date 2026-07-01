using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>Class that manages the mapping from GitHub user IDs to Discord user IDs.</summary>
public class GitHubUserMapManager(IConfiguration config, IHttpClientFactory httpClientFactory) : BaseManager<Dictionary<long, string>>(config, httpClientFactory), IGitHubUserMapManager
{
    protected override string? FilePath { get; } = config["GITHUB_USER_MAP_FILE_PATH"];

    protected override Uri? FileUrl { get; } = config["GITHUB_USER_MAP_FILE_URL"] is string url ? new Uri(url) : null;

    protected override string? BlobPath { get; } = config["GITHUB_USER_MAP_BLOB"];

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    // GitHub login name specification: alphanumerics and hyphens only, must start with an alphanumeric, max 39 characters.
    private static readonly Regex _loginRegex =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,38}$", RegexOptions.Compiled);

    protected override string GetDefaultFilePath() => "data/github-user-map.json";

    /// <summary>Returns an empty object as the default content for the user map.</summary>
    protected override string GetDefaultContent() => "{}";

    /// <inheritdoc/>
    protected override Dictionary<long, string>? Deserialize(string json)
        => DeserializeJson<Dictionary<long, string>>(json);

    /// <summary>Gets the Discord user ID from a GitHub user ID.</summary>
    /// <param name="githubUserId">The GitHub user ID to look up.</param>
    /// <returns>The corresponding Discord user ID, or <see langword="null"/> if no mapping exists.</returns>
    public string? GetById(long githubUserId)
    {
        if (Data is null)
        {
            throw new InvalidOperationException(
                "GitHubUserMapManager is not loaded. Call EnsureLoadedAsync() first.");
        }

        return Data.TryGetValue(githubUserId, out var discordId) ? discordId : null;
    }

    /// <summary>
    /// Resolves a numeric ID from a username via the GitHub API and looks it up in the map.
    /// Validates the login name format before embedding it into the URL path (prevents path traversal).
    /// </summary>
    /// <param name="login">The GitHub login name to look up.</param>
    /// <returns>The corresponding Discord user ID, or <see langword="null"/> if no mapping exists or the user is not found.</returns>
    public async Task<string?> GetFromUsernameAsync(string login)
    {
        if (!_loginRegex.IsMatch(login))
            return null;

        HttpClient http = _httpClientFactory.CreateClient("github");
        GitHubUserResponse? user;
        try
        {
            user = await http.GetFromJsonAsync<GitHubUserResponse>($"/users/{login}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (user is null) return null;
        return GetById(user.Id);
    }

    private record GitHubUserResponse(
        [property: JsonPropertyName("id")] long Id);
}
