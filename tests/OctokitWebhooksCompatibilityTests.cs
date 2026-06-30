using System.Reflection;
using System.Text.RegularExpressions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Octokit.Webhooks のトップレベルイベント型が既知リストと一致することを検証する。
/// Renovate による NuGet 更新後にこのテストが落ちた場合:
///   1. UPDATE_KNOWN_EVENTS=1 dotnet test --filter OctokitWebhooksCompatibilityTests を実行
///   2. tests/Snapshots/octokit-known-events.txt の差分を確認
///   3. 新イベントは実装 or コメントで「意図的に未実装」を記録してからコミット
/// </summary>
public class OctokitWebhooksCompatibilityTests
{
    private static readonly string KnownEventsPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Snapshots",
        "octokit-known-events.txt");

    [Fact]
    public void OctokitWebhookEventTypes_MustMatchKnownList()
    {
        var known = File.ReadAllLines(KnownEventsPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var actual = typeof(WebhookEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WebhookEvent)) && !t.IsAbstract)
            .Select(GetEventName)
            .ToHashSet(StringComparer.Ordinal);

        if (Environment.GetEnvironmentVariable("UPDATE_KNOWN_EVENTS") == "1")
        {
            File.WriteAllLines(KnownEventsPath, actual.OrderBy(n => n));
            return;
        }

        var added = actual.Except(known).OrderBy(n => n).ToList();
        var removed = known.Except(actual).OrderBy(n => n).ToList();

        Assert.True(added.Count == 0,
            $"Octokit.Webhooks に新しいイベント型が追加されました。実装するか既知リストに追記してください: {string.Join(", ", added)}");
        Assert.True(removed.Count == 0,
            $"Octokit.Webhooks からイベント型が削除されました。既知リストから除去してください: {string.Join(", ", removed)}");
    }

    /// <summary>
    /// イベント型からイベント名を取得する（型名 → snake_case 変換）。
    /// </summary>
    private static string GetEventName(Type t)
    {
        var name = t.Name;
        if (name.EndsWith("Event", StringComparison.Ordinal))
            name = name[..^5];
        if (name.EndsWith("Webhook", StringComparison.Ordinal))
            name = name[..^7];
        return Regex.Replace(name, "([a-z0-9])([A-Z])|([A-Z]+)([A-Z][a-z])", "$1$3_$2$4")
                    .ToLowerInvariant();
    }
}
