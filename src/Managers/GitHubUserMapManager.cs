using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>GitHub ユーザー ID から Discord ユーザー ID へのマッピングを管理するクラス。</summary>
public class GitHubUserMapManager(IConfiguration config, IHttpClientFactory httpClientFactory) : BaseManager<Dictionary<long, string>>(config, httpClientFactory), IGitHubUserMapManager
{
    protected override string? FilePath { get; } = config["GITHUB_USER_MAP_FILE_PATH"];

    protected override Uri? FileUrl { get; } = config["GITHUB_USER_MAP_FILE_URL"] is string url ? new Uri(url) : null;

    protected override string? BlobPath { get; } = config["GITHUB_USER_MAP_BLOB"];

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    // GitHub ログイン名の仕様: 英数字とハイフンのみ、先頭は英数字、最大 39 文字
    private static readonly Regex _loginRegex =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,38}$", RegexOptions.Compiled);

    protected override string GetDefaultFilePath() => "data/github-user-map.json";

    /// <summary>ユーザーマップのデフォルト内容として空オブジェクトを返す。</summary>
    protected override string GetDefaultContent() => "{}";

    /// <inheritdoc/>
    protected override Dictionary<long, string>? Deserialize(string json)
        => DeserializeJson<Dictionary<long, string>>(json);

    /// <summary>GitHub ユーザー ID から Discord ユーザー ID を取得する。</summary>
    /// <param name="githubUserId">検索対象の GitHub ユーザー ID</param>
    /// <returns>対応する Discord ユーザー ID。マッピングが存在しない場合は <see langword="null"/>。</returns>
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
    /// GitHub API でユーザー名から数値 ID を引き、マップを検索する。
    /// ログイン名を URL パスに埋め込む前に形式を検証する（パストラバーサル防止）。
    /// </summary>
    /// <param name="login">検索対象の GitHub ログイン名</param>
    /// <returns>対応する Discord ユーザー ID。マッピングが存在しない、またはユーザーが見つからない場合は <see langword="null"/>。</returns>
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
