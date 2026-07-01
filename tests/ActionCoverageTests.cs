using System.Reflection;
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Ensures that every implemented action annotated with <see cref="GitHubEventAttribute"/>
/// has a corresponding test class.
/// A failure of this test means an action exists without any tests.
/// </summary>
public class ActionCoverageTests
{
    /// <summary>
    /// Verifies that a matching *Tests class exists in the test assembly for every concrete
    /// action class annotated with <see cref="GitHubEventAttribute"/>.
    /// </summary>
    [Fact]
    public void AllGitHubEventAnnotatedActionsHaveTestClass()
    {
        // Collect concrete classes annotated with [GitHubEvent] from the main assembly
        Assembly mainAssembly = typeof(IAction).Assembly;
        Type[] implementedActions = mainAssembly.GetTypes()
            .Where(t =>
                t.GetCustomAttribute<GitHubEventAttribute>() != null &&
                t.IsClass &&
                !t.IsAbstract)
            .ToArray();

        // Collect *Tests classes from the test assembly
        Assembly testAssembly = typeof(ActionCoverageTests).Assembly;
        HashSet<string> testClassNames = testAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        // Enumerate actions that have no test class
        List<string> uncovered = implementedActions
            .Where(a => !testClassNames.Contains($"{a.Name}Tests"))
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            $"The following implemented actions have no test class:{Environment.NewLine}" +
            string.Join(Environment.NewLine, uncovered.Select(n => $"  - {n} → {n}Tests.cs required")));
    }
}
