namespace GitHubWebhookBridge.Managers;

/// <summary>GitHub ユーザー ID と Discord ユーザー ID のマッピングを定義するインターフェース。</summary>
public interface IGitHubUserMapManager
{
    /// <summary>初回呼び出し時のみデータをロードする（二重初期化防止）。</summary>
    /// <returns>処理完了を表す <see cref="Task"/>。</returns>
    Task EnsureLoadedAsync();

    /// <summary>GitHub ユーザー ID から Discord ユーザー ID を取得する。</summary>
    /// <param name="githubUserId">検索対象の GitHub ユーザー ID</param>
    /// <returns>対応する Discord ユーザー ID。マッピングが存在しない場合は <see langword="null"/>。</returns>
    string? GetById(long githubUserId);

    /// <summary>GitHub API でユーザー名から数値 ID を引き、マップを検索する。</summary>
    /// <param name="login">検索対象の GitHub ログイン名</param>
    /// <returns>対応する Discord ユーザー ID。マッピングが存在しない場合は <see langword="null"/>。</returns>
    Task<string?> GetFromUsernameAsync(string login);
}
