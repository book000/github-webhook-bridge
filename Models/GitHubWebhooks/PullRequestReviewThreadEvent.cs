using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook pull_request_review_thread イベントのペイロード
    /// </summary>
    public class PullRequestReviewThreadEvent
    {
        /// <summary>アクション種別（resolved/unresolved）</summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>対象のレビュースレッド</summary>
        [JsonPropertyName("thread")]
        public ReviewThread Thread { get; set; } = new();

        /// <summary>スレッドが属するプルリクエスト</summary>
        [JsonPropertyName("pull_request")]
        public PullRequest PullRequest { get; set; } = new();

        /// <summary>対象リポジトリ</summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        /// <summary>アクションを実行したユーザー</summary>
        [JsonPropertyName("sender")]
        public User Sender { get; set; } = new();
    }
}
