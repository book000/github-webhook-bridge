using System.Text.Json;
using Azure.Storage.Queues.Models;
using GitHubWebhookBridge.Services;
using Microsoft.Azure.Functions.Worker;

namespace GitHubWebhookBridge.Functions;

/// <summary>Queue trigger that delivers events accepted by <see cref="WebhookFunction"/> to Discord.</summary>
public class ProcessWebhookEventFunction(WebhookEventProcessor processor)
{
    private readonly WebhookEventProcessor _processor = processor;

    /// <summary>Deserializes the queue envelope and hands it to <see cref="WebhookEventProcessor"/>.</summary>
    /// <param name="message">The dequeued message.</param>
    /// <param name="cancellationToken">Cancellation token signalled on host shutdown.</param>
    /// <returns>A <see cref="Task"/> that completes when the message may be deleted.</returns>
    [Function("ProcessWebhookEvent")]
    public Task RunAsync(
        [QueueTrigger(QueueNames.Events, Connection = "AzureWebJobsStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The receiver is the only writer, so an undeserializable envelope is a host-level failure:
        // it is retried and then poisoned by the queue host.
        WebhookQueueMessage envelope = JsonSerializer.Deserialize<WebhookQueueMessage>(message.Body.ToString())
            ?? throw new InvalidOperationException($"Queue message {message.MessageId} is not a webhook event envelope.");
        return _processor.ProcessAsync(envelope, message.DequeueCount, cancellationToken);
    }
}
