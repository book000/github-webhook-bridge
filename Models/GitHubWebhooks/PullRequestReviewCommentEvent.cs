using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook pull_request_review_comment イベントのペイロード
    /// </summary>
    public class PullRequestReviewCommentEvent
    {
        /// <summary>アクション種別（created/edited/deleted）</summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>追加/変更されたレビューコメント</summary>
        [JsonPropertyName("comment")]
        public ReviewComment Comment { get; set; } = new();

        /// <summary>コメントが付いたプルリクエスト</summary>
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
