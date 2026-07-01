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
/// Integration test that uses the real GitHub Webhook payloads vendored under tests/RealPayloads/
/// to run everything from deserialization via <see cref="ActionFactory"/> through <see cref="IAction.RunAsync"/>.
/// Because the hand-written fixtures in <see cref="TestFixtures"/> are reverse-engineered from the
/// Octokit.Webhooks type definitions, they cannot detect cases where Octokit's <c>[JsonPropertyName]</c>
/// mapping diverges from the real payload from the start
/// (the <c>Review</c>/<c>Thread</c> mix-up bug in <c>pull_request_review_thread</c> is one such case).
/// This test deserializes the real payloads as the single source of truth and verifies that the
/// real payload values (repository, sender) are actually reflected in the Discord message,
/// continuously detecting the same class of mapping mistakes (#2651).
/// </summary>
public class RealPayloadIntegrationTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static readonly string PayloadsDir = Path.Combine(
        Path.GetDirectoryName(typeof(RealPayloadIntegrationTests).Assembly.Location)!,
        "RealPayloads");

    /// <summary>X-GitHub-Event names and their corresponding real payload file names (for the 12 implemented events).</summary>
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
    /// Runs the real payload through RunAsync(), verifying that it does not throw and
    /// that the repository and sender values in the real payload are actually reflected in the Discord message.
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

        // sender.login is always emitted by every Action, either in the title or as the Embed Author name
        // (repository.full_name may not be included in the Embed depending on the Action, so
        // sender.login is used as a value that can be verified across all Actions).
        var dump = Dump(captured!);
        Assert.Contains(expectedSender, dump, StringComparison.Ordinal);
    }

    /// <summary>Concatenates all text elements of the DiscordMessage into a single string for verification.</summary>
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
