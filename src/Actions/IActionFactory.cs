namespace GitHubWebhookBridge.Actions;

/// <summary>イベント名から IAction を生成するファクトリインターフェース。</summary>
public interface IActionFactory
{
    /// <summary>イベント名と生 JSON から適切な <see cref="IAction"/> インスタンスを生成して返す。</summary>
    /// <param name="eventName">GitHub Webhook の X-GitHub-Event ヘッダー値（小文字）</param>
    /// <param name="rawJson">Webhook ペイロードの生 JSON 文字列</param>
    /// <param name="webhookUrl">通知先 Discord Webhook URL</param>
    /// <returns>イベントに対応する <see cref="IAction"/> インスタンス。</returns>
    IAction GetAction(string eventName, string rawJson, Uri webhookUrl);
}
