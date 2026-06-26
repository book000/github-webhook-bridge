using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub Webhook ping イベントのペイロード
    /// </summary>
    public class PingEvent
    {
        /// <summary>GitHub からのランダムな文字列</summary>
        [JsonPropertyName("zen")]
        public string Zen { get; set; } = string.Empty;

        /// <summary>Webhook フック ID</summary>
        [JsonPropertyName("hook_id")]
        public long HookId { get; set; }
    }
}
