using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks;

/// <summary>
/// GitHub Webhook discussion イベントのペイロード
/// </summary>
public class DiscussionEvent
{
    /// <summary>アクション種別（created/edited/deleted/pinned/unpinned/locked/unlocked/transferred/answered/unanswered/labeled/unlabeled/category_changed 等）</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>対象ディスカッション</summary>
    [JsonPropertyName("discussion")]
    public Discussion Discussion { get; set; } = new();

    /// <summary>対象リポジトリ</summary>
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();

    /// <summary>アクションを実行したユーザー</summary>
    [JsonPropertyName("sender")]
    public User Sender { get; set; } = new();

    /// <summary>関連するコメント（answered/unanswered 時など）</summary>
    [JsonPropertyName("comment")]
    public DiscussionComment? Comment { get; set; }

    /// <summary>追加/削除されたラベル（labeled/unlabeled 時）</summary>
    [JsonPropertyName("label")]
    public Label? Label { get; set; }

    /// <summary>変更後のカテゴリ（category_changed 時）</summary>
    [JsonPropertyName("category")]
    public DiscussionCategory? Category { get; set; }
}
