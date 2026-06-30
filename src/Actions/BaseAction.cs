using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// 全 Action ハンドラーの抽象基底クラス。
/// Discord メッセージの送信と 5 分間キャッシュによる編集機能を提供する。
/// </summary>
public abstract class BaseAction<TEvent>(
    IDiscordClient discord,
    Uri webhookUrl,
    string eventName,
    TEvent @event,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger logger) : IAction
    where TEvent : WebhookEvent
{
    /// <summary>Discord Webhook API クライアントを取得する。</summary>
    protected IDiscordClient Discord { get; } = discord;

    /// <summary>通知先 Discord Webhook URL を取得する。</summary>
    protected Uri WebhookUrl { get; } = webhookUrl;

    /// <summary>GitHub Webhook イベント名を取得する。</summary>
    protected string EventName { get; } = eventName;

    /// <summary>デシリアライズされた Webhook ペイロードを取得する。</summary>
    protected TEvent Event { get; } = @event;

    /// <summary>GitHub → Discord ユーザーマッピングマネージャーを取得する。</summary>
    protected IGitHubUserMapManager UserMapManager { get; } = userMapManager;

    /// <summary>ロガーインスタンスを取得する。</summary>
    protected ILogger Logger { get; } = logger;

    private readonly IMessageCacheService _cache = cache;

    /// <summary>イベント処理を実行する。各サブクラスで実装する。</summary>
    public abstract Task RunAsync();

    /// <summary>
    /// Discord にメッセージを送信する。
    /// 同一キーのメッセージが 5 分以内に存在する場合は編集する。
    /// 全メッセージに SuppressNotifications フラグを付与する。
    /// </summary>
    /// <param name="key">キャッシュの検索・保存に使用するキー文字列</param>
    /// <param name="message">送信する Discord メッセージ</param>
    protected async Task SendMessageAsync(string key, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // SuppressNotifications フラグを付加（既存フラグは保持）
        message = message with { Flags = message.Flags | DiscordMessageFlags.SuppressNotifications };

        CachedMessage? cached = await _cache.GetAsync(WebhookUrl, key);
        if (cached is not null)
        {
            try
            {
                await Discord.EditMessageAsync(WebhookUrl, cached.MessageId, message);
                return;
            }
            catch (Exception ex)
            {
                // 編集失敗時（メッセージ削除済み等）はキャッシュを破棄して新規送信にフォールバック
                Logger.LogWarning(ex, "Failed to edit message {MessageId}, falling back to send.", cached.MessageId);
                await _cache.DeleteAsync(WebhookUrl, key);
            }
        }

        var messageId = await Discord.SendMessageAsync(WebhookUrl, message);
        await _cache.SetAsync(WebhookUrl, key, messageId);
    }

    /// <summary>
    /// GitHub ユーザー ID 一覧から Discord メンション文字列を生成する。
    /// 送信者自身は除外する。Team オブジェクトが含まれる場合は事前にフィルタリングすること。
    /// </summary>
    /// <param name="senderId">送信者の GitHub ユーザー ID（メンションから除外される）</param>
    /// <param name="users">メンション対象の GitHub ユーザー ID とログイン名のコレクション</param>
    /// <returns>Discord メンション文字列（スペース区切り）。対象がいない場合は空文字列。</returns>
    protected async Task<string> GetUsersMentionsAsync(
        long senderId,
        IEnumerable<(long Id, string Login)> users)
    {
        await UserMapManager.EnsureLoadedAsync();
        IEnumerable<string> mentions = users
            .Where(u => u.Id != senderId)
            .Select(u => UserMapManager.GetById(u.Id))
            .Where(discordId => discordId is not null)
            .Select(discordId => $"<@{discordId}>");
        return string.Join(" ", mentions);
    }

    /// <summary>
    /// 2 つのテキスト間の unified diff を生成する（DiffPlex InlineDiffBuilder 使用）。
    /// +/-/スペース 行プレフィックス形式。呼び出し元で ```diff コードブロックで囲むこと。
    /// </summary>
    /// <param name="oldText">変更前のテキスト</param>
    /// <param name="newText">変更後のテキスト</param>
    /// <param name="fileName">diff ヘッダーに表示するファイル名</param>
    /// <returns>+/-/スペース 行プレフィックス形式の diff 文字列。</returns>
    protected static string CreatePatch(string oldText, string newText, string fileName = "file")
    {
        DiffPaneModel diff = InlineDiffBuilder.Diff(oldText, newText);
        var sb = new StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"--- {fileName}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"+++ {fileName}");

        foreach (DiffPiece? line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+",
                ChangeType.Deleted => "-",
                ChangeType.Unchanged => throw new NotImplementedException(),
                ChangeType.Imaginary => throw new NotImplementedException(),
                ChangeType.Modified => throw new NotImplementedException(),
                _ => " ",
            };
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{prefix} {line.Text}");
        }

        return sb.ToString();
    }
}
