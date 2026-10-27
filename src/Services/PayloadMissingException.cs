namespace GitHubWebhookBridge.Services;

/// <summary>Thrown when the payload of a queued event cannot be found. Retrying cannot fix it.</summary>
public sealed class PayloadMissingException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PayloadMissingException"/> class.</summary>
    public PayloadMissingException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PayloadMissingException"/> class.</summary>
    /// <param name="message">The error message. It must not contain a webhook URL.</param>
    public PayloadMissingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PayloadMissingException"/> class.</summary>
    /// <param name="message">The error message. It must not contain a webhook URL.</param>
    /// <param name="innerException">The underlying exception.</param>
    public PayloadMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
