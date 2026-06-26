using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook push イベントのペイロード
    /// </summary>
    public class PushEvent
    {
        /// <summary>プッシュされたブランチ/タグの ref</summary>
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;

        /// <summary>プッシュ前のコミット SHA</summary>
        [JsonPropertyName("before")]
        public string Before { get; set; } = string.Empty;

        /// <summary>プッシュ後のコミット SHA</summary>
        [JsonPropertyName("after")]
        public string After { get; set; } = string.Empty;

        /// <summary>対象リポジトリ</summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new();

        /// <summary>プッシュを実行したユーザー</summary>
        [JsonPropertyName("sender")]
        public User Sender { get; set; } = new();

        /// <summary>プッシュに含まれるコミット一覧</summary>
        [JsonPropertyName("commits")]
        public List<Commit> Commits { get; set; } = new();

        /// <summary>変更比較ページ URL</summary>
        [JsonPropertyName("compare")]
        public string Compare { get; set; } = string.Empty;

        /// <summary>強制プッシュかどうか</summary>
        [JsonPropertyName("forced")]
        public bool Forced { get; set; }

        /// <summary>GitHub App インストール情報（オプション）</summary>
        [JsonPropertyName("installation")]
        public Installation? Installation { get; set; }
    }
}
