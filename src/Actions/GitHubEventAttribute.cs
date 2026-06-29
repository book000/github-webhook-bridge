namespace GitHubWebhookBridge.Actions;

/// <summary>
/// GitHub Webhook イベントハンドラークラスをイベント名に紐付ける属性。
/// <para>
/// Level 1（明示的な定数）: <c>[GitHubEvent(WebhookEventType.PullRequest)]</c><br/>
/// Level 2（自動導出）: <c>[GitHubEvent]</c> — ペイロード型の Octokit 属性からイベント名を導出する。
/// Task 1 の調査で Level 2 が使えない場合は Level 1 のみ使用する。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GitHubEventAttribute : Attribute
{
    /// <summary>イベント名を明示指定する Level 1 コンストラクター。</summary>
    /// <param name="eventName">GitHub Webhook イベント名（小文字スネークケース）。</param>
    public GitHubEventAttribute(string eventName) => EventName = eventName;

    /// <summary>ペイロード型から自動導出する Level 2 コンストラクター。</summary>
    public GitHubEventAttribute() => EventName = null;

    /// <summary>
    /// Level 1 で指定されたイベント名。Level 2 の場合は <see langword="null"/>。
    /// </summary>
    public string? EventName { get; }
}
