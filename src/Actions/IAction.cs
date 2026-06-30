namespace GitHubWebhookBridge.Actions;

/// <summary>全 GitHub Webhook イベントハンドラーの共通動作を定義するインターフェース</summary>
public interface IAction
{
    /// <summary>Webhook イベントを処理し、Discord に通知する</summary>
    /// <returns>処理完了を表す <see cref="Task"/></returns>
    Task RunAsync();
}
