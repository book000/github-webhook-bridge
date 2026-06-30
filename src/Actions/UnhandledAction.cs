namespace GitHubWebhookBridge.Actions;

/// <summary>
/// 未実装イベントのフォールバックハンドラークラス。
/// <see cref="GitHubEventAttribute"/> を持たないため <see cref="ActionFactory"/> のレジストリに登録されない。
/// 呼び出されると常に <see cref="NotImplementedException"/> をスローし、
/// <see cref="GitHubWebhookBridge.Functions.WebhookFunction"/> が HTTP 406 に変換する
/// </summary>
public sealed class UnhandledAction(string eventName) : IAction
{
    /// <inheritdoc/>
    public Task RunAsync()
        => throw new NotImplementedException($"Event '{eventName}' is not implemented.");
}
