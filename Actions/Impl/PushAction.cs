using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>GitHub push イベントを Discord に通知する。</summary>
public sealed class PushAction : BaseAction<PushEvent>
{
    /// <inheritdoc cref="BaseAction{TEvent}"/>
    public PushAction(IDiscordClient d, string wu, string en, PushEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        var commits = Event.Commits;
        if (commits.Count == 0) return;

        // refs/heads/ や refs/tags/ を除去して短いブランチ名にする
        var shortRef = Event.Ref
            .Replace("refs/heads/", "")
            .Replace("refs/tags/", "");

        var description = GetDescription(commits);

        var author = new DiscordEmbedAuthor(
            Name:    Event.Sender.Login,
            Url:     Event.Sender.HtmlUrl,
            IconUrl: Event.Sender.AvatarUrl);

        var embed = EmbedHelper.CreateEmbed(
            eventName:   EventName,
            color:       EmbedColors.Push,
            title:       $"[{Event.Repository.FullName}:{shortRef}] {commits.Count} new commit(s)",
            description: description,
            author:      author);

        var key = $"{Event.Repository.FullName}:{Event.Ref}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    /// <summary>コミット一覧の説明文を生成する。</summary>
    private static string GetDescription(List<Commit> commits)
    {
        return string.Join("\n", commits.Select(c =>
        {
            var shortSha = c.Id.Length >= 7 ? c.Id[..7] : c.Id;
            // 複数行メッセージは最初の行のみ使用する
            var firstLine = c.Message.Contains('\n')
                ? c.Message.Split('\n', 2)[0]
                : c.Message;
            var shortMessage = firstLine.Length > 50
                ? $"{firstLine[..50]}..."
                : firstLine;
            return $"[`{shortSha}`]({c.Url}) {shortMessage} - {c.Author.Name}";
        }));
    }
}
