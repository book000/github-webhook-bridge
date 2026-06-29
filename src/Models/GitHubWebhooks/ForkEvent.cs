using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks;

/// <summary>
/// GitHub Webhook fork イベントのペイロード
/// </summary>
public class ForkEvent
{
    /// <summary>フォークされた新しいリポジトリ</summary>
    [JsonPropertyName("forkee")]
    public Repository Forkee { get; set; } = new();

    /// <summary>フォーク元のリポジトリ</summary>
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();

    /// <summary>フォークを実行したユーザー</summary>
    [JsonPropertyName("sender")]
    public User Sender { get; set; } = new();
}
