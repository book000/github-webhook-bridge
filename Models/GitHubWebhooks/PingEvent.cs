using System.Collections.Generic;
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

        /// <summary>Webhook フック情報</summary>
        [JsonPropertyName("hook")]
        public PingHook Hook { get; set; } = new();

        /// <summary>Webhook が設定されたリポジトリ（リポジトリ Webhook の場合）</summary>
        [JsonPropertyName("repository")]
        public Repository? Repository { get; set; }

        /// <summary>Webhook をトリガーしたユーザー</summary>
        [JsonPropertyName("sender")]
        public User? Sender { get; set; }

        /// <summary>Webhook が設定された Organization（Org Webhook の場合）</summary>
        [JsonPropertyName("organization")]
        public PingOrganization? Organization { get; set; }
    }

    /// <summary>
    /// ping イベントのフック設定情報
    /// </summary>
    public class PingHook
    {
        /// <summary>フックタイプ（Repository/Organization/App）</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>フック ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>フックが購読しているイベント一覧</summary>
        [JsonPropertyName("events")]
        public List<string>? Events { get; set; }
    }

    /// <summary>
    /// ping イベントの Organization 情報
    /// </summary>
    public class PingOrganization
    {
        /// <summary>Organization のログイン名</summary>
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }
}
