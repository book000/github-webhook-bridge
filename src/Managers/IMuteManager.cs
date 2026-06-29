namespace GitHubWebhookBridge.Managers;

/// <summary>ユーザーミュート判定インターフェース。</summary>
public interface IMuteManager
{
    /// <summary>初回呼び出し時のみデータをロードする（二重初期化防止）。</summary>
    /// <returns>処理完了を表す <see cref="Task"/>。</returns>
    Task EnsureLoadedAsync();

    /// <summary>ユーザーが指定イベントでミュートされているかどうかを返す。</summary>
    /// <param name="userId">判定対象の GitHub ユーザー ID。</param>
    /// <param name="eventName">GitHub Webhook イベント名。</param>
    /// <param name="action">イベントのアクション種別（省略可）。</param>
    /// <returns>ミュート対象の場合は true、それ以外は false。</returns>
    bool IsMuted(long userId, string eventName, string? action);
}
