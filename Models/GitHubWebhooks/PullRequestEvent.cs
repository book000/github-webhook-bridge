using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook pull_request イベントのペイロード
    /// </summary>
    public class PullRequestEvent
    {
        /// <summary>アクション種別（opened/closed/reopened/edited/labeled/unlabeled/assigned/unassigned/review_requested/review_request_removed/synchronize 等）</summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>プルリクエスト番号</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>対象プルリクエスト</summary>
        [JsonPropertyName("pull_request")]
        public PullRequest PullRequest { get; set; } = new();

        /// <summary>対象リポジトリ</summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        /// <summary>アクションを実行したユーザー</summary>
        [JsonPropertyName("sender")]
        public User Sender { get; set; } = new();

        /// <summary>追加/削除されたラベル（labeled/unlabeled 時）</summary>
        [JsonPropertyName("label")]
        public Label? Label { get; set; }

        /// <summary>アサイン/アンアサインされたユーザー（assigned/unassigned 時）</summary>
        [JsonPropertyName("assignee")]
        public User? Assignee { get; set; }

        /// <summary>レビューリクエストされたユーザー（review_requested 時）</summary>
        [JsonPropertyName("requested_reviewer")]
        public User? RequestedReviewer { get; set; }

        /// <summary>編集前後の変更内容（edited 時）</summary>
        [JsonPropertyName("changes")]
        public JsonElement? Changes { get; set; }

        /// <summary>同期前のコミット SHA（synchronize 時）</summary>
        [JsonPropertyName("before")]
        public string? Before { get; set; }

        /// <summary>同期後のコミット SHA（synchronize 時）</summary>
        [JsonPropertyName("after")]
        public string? After { get; set; }
    }
}
