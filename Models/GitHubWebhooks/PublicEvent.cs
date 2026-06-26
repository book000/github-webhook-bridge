using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks;

/// <summary>
/// GitHub Webhook public イベントのペイロード（リポジトリの公開）
/// </summary>
public class PublicEvent
{
    /// <summary>公開されたリポジトリ</summary>
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();

    /// <summary>アクションを実行したユーザー</summary>
    [JsonPropertyName("sender")]
    public User Sender { get; set; } = new();
}
