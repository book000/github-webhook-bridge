namespace GitHubWebhookBridge.Actions;

/// <summary>
/// GitHub Webhook イベントハンドラークラスをイベント名に紐付ける属性クラス。
/// <para>
/// 使用例: <c>[GitHubEvent(WebhookEventType.PullRequest)]</c>
/// </para>
/// </summary>
/// <param name="eventName">GitHub Webhook イベント名（小文字スネークケース）</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute(string eventName) : Attribute
{
    /// <summary>
    /// <c>[GitHubEvent]</c> 属性で指定されたイベント名を取得する
    /// </summary>
    public string EventName { get; } = eventName;
}
