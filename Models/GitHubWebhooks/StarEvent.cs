using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook star イベントのペイロード
    /// </summary>
    public class StarEvent
    {
        /// <summary>アクション種別（created/deleted）</summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>スターが付けられた日時（削除時は null）</summary>
        [JsonPropertyName("starred_at")]
        public string? StarredAt { get; set; }

        /// <summary>対象リポジトリ</summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        /// <summary>アクションを実行したユーザー</summary>
        [JsonPropertyName("sender")]
        public User Sender { get; set; } = new();
    }
}
