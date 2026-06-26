using System.Text.Json;

namespace GitHubWebhookBridge.Actions;

/// <summary>イベント名から IAction を生成するファクトリインターフェース。</summary>
public interface IActionFactory
{
    IAction GetAction(string eventName, JsonElement body, Uri webhookUrl);
}
