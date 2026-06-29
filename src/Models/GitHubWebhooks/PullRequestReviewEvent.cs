using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks;

/// <summary>
/// GitHub Webhook pull_request_review イベントのペイロード
/// </summary>
public class PullRequestReviewEvent
{
    /// <summary>アクション種別（submitted/edited/dismissed）</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>提出されたレビュー</summary>
    [JsonPropertyName("review")]
    public Review Review { get; set; } = new();

    /// <summary>レビュー対象のプルリクエスト</summary>
    [JsonPropertyName("pull_request")]
    public PullRequest PullRequest { get; set; } = new();

    /// <summary>対象リポジトリ</summary>
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();

    /// <summary>アクションを実行したユーザー</summary>
    [JsonPropertyName("sender")]
    public User Sender { get; set; } = new();
}
