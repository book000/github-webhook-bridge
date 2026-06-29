using System.Text.Json;

namespace GitHubWebhookBridge.Actions;

/// <summary>イベント名から IAction を生成するファクトリインターフェース。</summary>
public interface IActionFactory
{
    /// <summary>イベント名から適切な <see cref="IAction"/> インスタンスを生成して返す。</summary>
    /// <param name="eventName">GitHub Webhook の X-GitHub-Event ヘッダー値。</param>
    /// <param name="body">Webhook ペイロードの JSON 要素。</param>
    /// <param name="webhookUrl">通知先 Discord Webhook URL。</param>
    /// <returns>イベントに対応する <see cref="IAction"/> インスタンス。</returns>
    IAction GetAction(string eventName, JsonElement body, Uri webhookUrl);
}
