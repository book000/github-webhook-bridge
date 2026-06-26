using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook issue_comment イベントのペイロード
    /// </summary>
    public class IssueCommentEvent
    {
        /// <summary>アクション種別（created/edited/deleted）</summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>コメントが投稿された Issue</summary>
        [JsonPropertyName("issue")]
        public Issue Issue { get; set; } = new();

        /// <summary>追加/変更されたコメント</summary>
        [JsonPropertyName("comment")]
        public Comment Comment { get; set; } = new();

        /// <summary>対象リポジトリ</summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        /// <summary>アクションを実行したユーザー</summary>
        [JsonPropertyName("sender")]
        public User Sender { get; set; } = new();
    }
}
