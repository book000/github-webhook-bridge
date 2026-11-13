namespace GitHubWebhookBridge.Actions;

/// <summary>
/// The verbatim webhook body, for actions that need fields Octokit.Webhooks does not model.
/// <see cref="ActionFactory"/> passes it only to actions whose constructor declares a parameter of this type.
/// </summary>
/// <param name="Json">The raw JSON text of the webhook payload.</param>
public sealed record RawPayload(string Json);
