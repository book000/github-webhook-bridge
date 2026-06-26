using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>GitHub ユーザー ID から Discord ユーザー ID へのマッピングを管理する。</summary>
public class GitHubUserMapManager : BaseManager<Dictionary<long, string>>, IGitHubUserMapManager
{
    protected override string? FilePath { get; }
    protected override string? FileUrl { get; }
    protected override string? BlobPath { get; }

    private readonly IHttpClientFactory _httpClientFactory;

    // GitHub ログイン名の仕様: 英数字とハイフンのみ、先頭は英数字、最大 39 文字
    private static readonly Regex LoginRegex =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,38}$", RegexOptions.Compiled);

    public GitHubUserMapManager(IConfiguration config, IHttpClientFactory httpClientFactory)
        : base(config, httpClientFactory)
    {
        FilePath = config["GITHUB_USER_MAP_FILE_PATH"];
        FileUrl = config["GITHUB_USER_MAP_FILE_URL"];
        BlobPath = config["GITHUB_USER_MAP_BLOB"];
        _httpClientFactory = httpClientFactory;
    }

    protected override string GetDefaultFilePath() => "data/github-user-map.json";

    protected override Dictionary<long, string>? Deserialize(string json)
        => DeserializeJson<Dictionary<long, string>>(json);

    /// <summary>GitHub ユーザー ID から Discord ユーザー ID を取得する。</summary>
    public string? Get(long githubUserId)
    {
        if (Data is null)
            throw new InvalidOperationException(
                "GitHubUserMapManager is not loaded. Call EnsureLoadedAsync() first.");
        return Data.TryGetValue(githubUserId, out var discordId) ? discordId : null;
    }

    /// <summary>
    /// GitHub API でユーザー名から数値 ID を引き、マップを検索する。
    /// ログイン名を URL パスに埋め込む前に形式を検証する（パストラバーサル防止）。
    /// </summary>
    public async Task<string?> GetFromUsernameAsync(string login)
    {
        if (!LoginRegex.IsMatch(login))
            return null;

        var http = _httpClientFactory.CreateClient("github");
        var user = await http.GetFromJsonAsync<GitHubUserResponse>($"/users/{login}");
        if (user is null) return null;
        return Get(user.Id);
    }

    private record GitHubUserResponse(
        [property: JsonPropertyName("id")] long Id);
}
