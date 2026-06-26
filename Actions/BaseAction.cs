using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions;

/// <summary>
/// 全 Action ハンドラーの抽象基底クラス。
/// Discord メッセージの送信と 5 分間キャッシュによる編集機能を提供する。
/// </summary>
public abstract class BaseAction<TEvent> : IAction
{
    protected readonly IDiscordClient        Discord;
    protected readonly string                WebhookUrl;
    protected readonly string                EventName;
    protected readonly TEvent                Event;
    protected readonly IGitHubUserMapManager UserMapManager;
    protected readonly ILogger               Logger;
    private   readonly IMessageCacheService  _cache;

    protected BaseAction(
        IDiscordClient        discord,
        string                webhookUrl,
        string                eventName,
        TEvent                @event,
        IMessageCacheService  cache,
        IGitHubUserMapManager userMapManager,
        ILogger               logger)
    {
        Discord        = discord;
        WebhookUrl     = webhookUrl;
        EventName      = eventName;
        Event          = @event;
        _cache         = cache;
        UserMapManager = userMapManager;
        Logger         = logger;
    }

    /// <summary>イベント処理を実行する。各サブクラスで実装する。</summary>
    public abstract Task RunAsync();

    /// <summary>
    /// Discord にメッセージを送信する。
    /// 同一キーのメッセージが 5 分以内に存在する場合は編集する。
    /// 全メッセージに SuppressNotifications フラグを付与する。
    /// </summary>
    protected async Task SendMessageAsync(string key, DiscordMessage message)
    {
        // SuppressNotifications フラグを付加（既存フラグは保持）
        message = message with { Flags = message.Flags | DiscordMessageFlags.SuppressNotifications };

        var cached = await _cache.GetAsync(WebhookUrl, key);
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
    protected async Task<string> GetUsersMentionsAsync(
        long senderId,
        IEnumerable<(long Id, string Login)> users)
    {
        await UserMapManager.EnsureLoadedAsync();
        var mentions = users
            .Where(u => u.Id != senderId)
            .Select(u => UserMapManager.Get(u.Id))
            .Where(discordId => discordId is not null)
            .Select(discordId => $"<@{discordId}>");
        return string.Join(" ", mentions);
    }

    /// <summary>
    /// 2 つのテキスト間の unified diff を生成する（DiffPlex InlineDiffBuilder 使用）。
    /// TypeScript の diff.createPatch() と同等の +/-/スペース 行プレフィックス形式。
    /// 呼び出し元で ```diff コードブロックで囲むこと。
    /// </summary>
    protected static string CreatePatch(string oldText, string newText, string fileName = "file")
    {
        var diff = InlineDiffBuilder.Diff(oldText, newText);
        var sb   = new System.Text.StringBuilder();
        sb.AppendLine($"--- {fileName}");
        sb.AppendLine($"+++ {fileName}");

        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+",
                ChangeType.Deleted  => "-",
                _                   => " ",
            };
            sb.AppendLine($"{prefix} {line.Text}");
        }

        return sb.ToString();
    }
}
