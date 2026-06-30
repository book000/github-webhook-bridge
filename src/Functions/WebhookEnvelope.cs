using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// ミュートチェック用の最小ペイロードを表すレコード。送信者 ID と action フィールドのみ抽出する。
/// </summary>
internal sealed record WebhookEnvelope(
    [property: JsonPropertyName("sender")] WebhookSender? Sender,
    [property: JsonPropertyName("action")] string? Action);

/// <summary>送信者情報を表す最小のレコード。</summary>
internal sealed record WebhookSender(
    // GitHub API の sender.id は数値が大きいため long? を使用する
    [property: JsonPropertyName("id")] long? Id);
