using System.Reflection;
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// 実装済みアクション（Actions.Impl）すべてにテストクラスが存在することを保証する。
/// このテストが失敗した場合、テストのないアクションが存在することを意味する。
/// </summary>
public class ActionCoverageTests
{
    /// <summary>
    /// Actions.Impl 内の全具象アクションクラスに対応する *Tests クラスが
    /// テストアセンブリに存在することを検証する。
    /// </summary>
    [Fact]
    public void AllImplementedActionsHaveTestClass()
    {
        // 本体アセンブリから Actions.Impl の具象クラスを収集する（スタブを除外）
        Assembly mainAssembly = typeof(IAction).Assembly;
        Type[] implementedActions = mainAssembly.GetTypes()
            .Where(t =>
                t.Namespace == "GitHubWebhookBridge.Actions.Impl" &&
                t.IsClass &&
                !t.IsAbstract &&
                IsConcreteAction(t))
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
            $"以下の実装済みアクションにテストクラスがありません。" +
            $" 'Actions/Impl/*.cs' に追加したら 'tests/.../*Tests.cs' も追加してください:{Environment.NewLine}" +
            string.Join(Environment.NewLine, uncovered.Select(n => $"  - {n} → {n}Tests.cs が必要")));
    }

    /// <summary>型が BaseAction&lt;T&gt; を継承する具象クラスかどうかを判定する。</summary>
    private static bool IsConcreteAction(Type type)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition().FullName == "GitHubWebhookBridge.Actions.BaseAction`1")
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
