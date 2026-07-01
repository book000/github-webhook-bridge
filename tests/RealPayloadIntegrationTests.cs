using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// tests/RealPayloads/ に vendoring された実 GitHub Webhook ペイロードを用いて、
/// <see cref="ActionFactory"/> によるデシリアライズから <c>Action.RunAsync()</c> までを実行する統合テスト。
/// <see cref="TestFixtures"/> の手作りフィクスチャは Octokit.Webhooks の型定義から逆算しているため、
/// Octokit 側の <c>[JsonPropertyName]</c> マッピングが最初から実ペイロードと食い違っている場合は検出できない
/// （<c>pull_request_review_thread</c> の <c>Review</c>/<c>Thread</c> 取り違えバグがこのパターン）。
/// このテストは実ペイロードを唯一の正としてデシリアライズし、実ペイロードの値
/// （repository・sender）が Discord メッセージに実際に反映されることを検証することで、
/// 同種のマッピングミスを継続的に検出する（#2651）。
/// </summary>
public class RealPayloadIntegrationTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static readonly string PayloadsDir = Path.Combine(
        Path.GetDirectoryName(typeof(RealPayloadIntegrationTests).Assembly.Location)!,
        "RealPayloads");

    /// <summary>X-GitHub-Event 名と対応する実ペイロードファイル名（実装済み 12 イベント分）。</summary>
    public static TheoryData<string, string> Fixtures => new()
    {
        { "discussion", "discussion.created.json" },
        { "fork", "fork.json" },
        { "issue_comment", "issue_comment.created.json" },
        { "issues", "issues.opened.json" },
        { "ping", "ping.json" },
        { "public", "public.json" },
        { "pull_request", "pull_request.opened.json" },
        { "pull_request_review", "pull_request_review.submitted.json" },
        { "pull_request_review_comment", "pull_request_review_comment.created.json" },
        { "pull_request_review_thread", "pull_request_review_thread.resolved.json" },
        { "push", "push.json" },
        { "star", "star.created.json" },
    };

    /// <summary>
    /// 実ペイロードを RunAsync() まで通し、例外を投げないこと、
    /// かつ実ペイロード中の repository・sender の値が Discord メッセージに実際に反映されることを検証する。
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task RunAsyncReflectsRealPayloadFields(string eventName, string fileName)
    {
        var rawJson = await File.ReadAllTextAsync(Path.Combine(PayloadsDir, fileName));
        using var doc = JsonDocument.Parse(rawJson);
        var expectedSender = doc.RootElement.GetProperty("sender").GetProperty("login").GetString()!;

        DiscordMessage? captured = null;

        Mock<IDiscordClient> discord = new();
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => captured = msg)
               .ReturnsAsync("msg-id");

        Mock<IMessageCacheService> cache = new();
        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        Mock<IGitHubUserMapManager> userMap = new();
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(discord.Object);
        services.AddSingleton(cache.Object);
        services.AddSingleton(userMap.Object);

        using ServiceProvider sp = services.BuildServiceProvider();
        ActionFactory factory = new(sp);

        IAction action = factory.GetAction(eventName, rawJson, _webhookUri);
        Assert.IsNotType<UnhandledAction>(action);

        await action.RunAsync();

        Assert.NotNull(captured);

        // sender.login はどの Action でもタイトルまたは Embed の Author 名として必ず出力される
        // （repository.full_name は Action によって Embed に含めない場合があるため、
        // 全 Action 共通で検証できる値として sender.login を使用する）。
        var dump = Dump(captured!);
        Assert.Contains(expectedSender, dump, StringComparison.Ordinal);
    }

    /// <summary>DiscordMessage の全テキスト要素を検証用に 1 文字列へ連結する。</summary>
    private static string Dump(DiscordMessage message)
    {
        List<string?> parts = [message.Content];
        foreach (DiscordEmbed embed in message.Embeds ?? [])
        {
            parts.Add(embed.Title);
            parts.Add(embed.Description);
            parts.Add(embed.Author?.Name);
            foreach (DiscordEmbedField field in embed.Fields ?? [])
            {
                parts.Add(field.Name);
                parts.Add(field.Value);
            }
        }
        return string.Join("\n", parts.Where(p => p is not null));
    }
}
