using System.Reflection;
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// <see cref="GitHubEventAttribute"/> が付与された実装済みアクションすべてに
/// テストクラスが存在することを保証する。
/// このテストが失敗した場合、テストのないアクションが存在することを意味する。
/// </summary>
public class ActionCoverageTests
{
    /// <summary>
    /// <see cref="GitHubEventAttribute"/> 付きの全具象アクションクラスに対応する *Tests クラスが
    /// テストアセンブリに存在することを検証する。
    /// </summary>
    [Fact]
    public void AllGitHubEventAnnotatedActionsHaveTestClass()
    {
        // 本体アセンブリから [GitHubEvent] 付きの具象クラスを収集する
        Assembly mainAssembly = typeof(IAction).Assembly;
        Type[] implementedActions = mainAssembly.GetTypes()
            .Where(t =>
                t.GetCustomAttribute<GitHubEventAttribute>() != null &&
                t.IsClass &&
                !t.IsAbstract)
            .ToArray();

        // テストアセンブリから *Tests クラスを収集する
        Assembly testAssembly = typeof(ActionCoverageTests).Assembly;
        HashSet<string> testClassNames = testAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        // テストクラスが存在しないアクションを列挙する
        List<string> uncovered = implementedActions
            .Where(a => !testClassNames.Contains($"{a.Name}Tests"))
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            $"以下の実装済みアクションにテストクラスがありません:{Environment.NewLine}" +
            string.Join(Environment.NewLine, uncovered.Select(n => $"  - {n} → {n}Tests.cs が必要")));
    }
}
