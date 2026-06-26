using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks;

/// <summary>
/// GitHub Webhook issues イベントのペイロード
/// </summary>
public class IssuesEvent
{
    /// <summary>アクション種別（opened/closed/reopened/edited/labeled/unlabeled/assigned/unassigned/milestoned/demilestoned 等）</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>対象 Issue</summary>
    [JsonPropertyName("issue")]
    public Issue Issue { get; set; } = new();

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

    /// <summary>編集前後の変更内容（edited 時）</summary>
    [JsonPropertyName("changes")]
    public JsonElement? Changes { get; set; }

    /// <summary>関連するマイルストーン（milestoned/demilestoned 時）</summary>
    [JsonPropertyName("milestone")]
    public Milestone? Milestone { get; set; }
}
