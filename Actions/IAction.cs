namespace GitHubWebhookBridge.Actions;

/// <summary>全 GitHub Webhook イベントハンドラーの共通インターフェース。</summary>
public interface IAction
{
    Task RunAsync();
}
