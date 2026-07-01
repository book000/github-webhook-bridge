using System.Reflection;
using System.Text.RegularExpressions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Verifies that the top-level event types in Octokit.Webhooks match the known list.
/// If this test fails after a Renovate-driven NuGet update:
///   1. Run UPDATE_KNOWN_EVENTS=1 dotnet test --filter OctokitWebhooksCompatibilityTests
///   2. Review the diff in tests/Snapshots/octokit-known-events.txt
///   3. Either implement new events or record "intentionally unimplemented" in a comment before committing
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
            $"New event types were added to Octokit.Webhooks. Implement them or append to the known list: {string.Join(", ", added)}");
        Assert.True(removed.Count == 0,
            $"Event types were removed from Octokit.Webhooks. Remove them from the known list: {string.Join(", ", removed)}");
    }

    /// <summary>
    /// Derives the event name from the event type (type name -> snake_case conversion).
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
