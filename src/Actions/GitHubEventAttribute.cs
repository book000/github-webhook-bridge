namespace GitHubWebhookBridge.Actions;

/// <summary>
/// GitHub Webhook イベントハンドラークラスをイベント名に紐付ける属性クラス。
/// <para>
/// 使用例: <c>[GitHubEvent(WebhookEventType.PullRequest)]</c>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute : Attribute
{
    /// <summary>
    /// イベント名を明示指定するコンストラクター。
    /// </summary>
    /// <param name="eventName">GitHub Webhook イベント名（小文字スネークケース）。</param>
    public GitHubEventAttribute(string eventName) => EventName = eventName;

    /// <summary>
    /// <c>[GitHubEvent]</c> 属性で指定されたイベント名を取得する。
    /// </summary>
    public string EventName { get; }
}
