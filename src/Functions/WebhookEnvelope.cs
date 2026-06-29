using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// ミュートチェック用の最小ペイロード。送信者 ID と action フィールドのみ抽出する。
/// </summary>
internal sealed record WebhookEnvelope(
    [property: JsonPropertyName("sender")] WebhookSender? Sender,
    [property: JsonPropertyName("action")]  string?        Action);

/// <summary>送信者情報の最小表現。</summary>
internal sealed record WebhookSender(
    // scratch/octokit-api-surface.md の SenderIdType に合わせて long? を使用する
    [property: JsonPropertyName("id")] long? Id);
