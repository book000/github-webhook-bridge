using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>
/// Notifies Discord of GitHub secret scanning alert events.
/// The message is built only from the alert number, secret type slug, resolution, repository and sender;
/// the detected secret is never part of the message or the logs.
/// </summary>
/// <inheritdoc cref="BaseAction{TEvent}"/>
[GitHubEvent(WebhookEventType.SecretScanningAlert)]
public sealed class SecretScanningAlertAction(
    IDiscordClient discord,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger<SecretScanningAlertAction> logger,
    Uri webhookUrl,
    string eventName,
    SecretScanningAlertEvent secretScanningAlertEvent)
    : BaseAction<SecretScanningAlertEvent>(discord, webhookUrl, eventName, secretScanningAlertEvent, cache, userMapManager, logger)
{
    private const int MaxTitleLength = 256;
    private const int MaxFieldValueLength = 1024;

    /// <inheritdoc/>
    public override async Task RunAsync()
    {
        if (Event.Repository is not { } repo)
        {
            Logger.LogWarning("secret_scanning_alert payload is missing repository; skipping notification.");
            return;
        }

        (string Verb, string State, int Color)? mapped = Event.Action switch
        {
            "created" => ("created", "open", EmbedColors.SecretScanningAlertOpen),
            "publicly_leaked" => ("publicly leaked", "open", EmbedColors.SecretScanningAlertOpen),
            "reopened" => ("reopened", "open", EmbedColors.SecretScanningAlertReopened),
            "validated" => ("validated", "open", EmbedColors.SecretScanningAlertOpen),
            "resolved" => ("resolved", "resolved", EmbedColors.SecretScanningAlertResolved),
            "revoked" => ("revoked", "resolved", EmbedColors.SecretScanningAlertResolved),
            _ => null,
        };

        if (mapped is not var (verb, state, color))
        {
            Logger.LogInformation(
                "Skipping secret_scanning_alert action {Action} for {Repository}.", Event.Action, repo.FullName);
            return;
        }

        Octokit.Webhooks.Models.SecretScanningAlertEvent.Alert alert = Event.Alert;
        var secretType = Truncate(alert.SecretType, MaxFieldValueLength);
        var title = Truncate($"Secret scanning alert {verb}: #{alert.Number} ({secretType}) in {repo.FullName}", MaxTitleLength);

        var fields = new List<DiscordEmbedField>
        {
            new("Repository", Truncate(repo.FullName, MaxFieldValueLength), true),
            new("Alert", $"#{alert.Number}", true),
            new("Secret type", secretType, true),
            new("State", state, true),
        };

        if (alert.Resolution is { } resolution)
            fields.Add(new("Resolution", Truncate(resolution.StringValue, MaxFieldValueLength), true));

        Uri? alertUrl = Uri.TryCreate(repo.HtmlUrl, UriKind.Absolute, out Uri? repoUrl)
            ? new Uri($"{repoUrl.AbsoluteUri.TrimEnd('/')}/security/secret-scanning/{alert.Number}")
            : null;

        // GitHub raises "created" alerts itself, so the payload has no sender.
        DiscordEmbedAuthor? author = Event.Sender is { } sender
            ? new DiscordEmbedAuthor(
                Name: sender.Login,
                Url: Uri.TryCreate(sender.HtmlUrl, UriKind.Absolute, out Uri? senderUrl) ? senderUrl : null,
                IconUrl: Uri.TryCreate(sender.AvatarUrl, UriKind.Absolute, out Uri? avatarUrl) ? avatarUrl : null)
            : null;

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            eventName: EventName,
            color: color,
            title: title,
            url: alertUrl,
            author: author,
            fields: fields);

        var key = $"{repo.FullName}-secret-scanning-alert-{alert.Number}";
        await SendMessageAsync(key, new DiscordMessage(Embeds: [embed]));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length > maxLength ? value[..maxLength] : value;
}
