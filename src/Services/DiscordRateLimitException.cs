using System.Globalization;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// Thrown when Discord answers HTTP 429. The message never contains the webhook URL because it contains the token.
/// </summary>
public sealed class DiscordRateLimitException : HttpRequestException
{
    /// <summary>Wait time Discord asked for when the response carried no usable <c>Retry-After</c>.</summary>
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>Initializes a new instance of the <see cref="DiscordRateLimitException"/> class.</summary>
    public DiscordRateLimitException()
        : this(DefaultRetryAfter, isGlobal: false, scope: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscordRateLimitException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public DiscordRateLimitException(string message)
        : base(message) => RetryAfter = DefaultRetryAfter;

    /// <summary>Initializes a new instance of the <see cref="DiscordRateLimitException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public DiscordRateLimitException(string message, Exception innerException)
        : base(message, innerException) => RetryAfter = DefaultRetryAfter;

    /// <summary>Initializes a new instance of the <see cref="DiscordRateLimitException"/> class.</summary>
    /// <param name="retryAfter">How long Discord asked the caller to wait.</param>
    /// <param name="isGlobal">Whether the global rate limit was hit.</param>
    /// <param name="scope">The <c>X-RateLimit-Scope</c> value (<c>user</c>, <c>global</c> or <c>shared</c>), if any.</param>
    public DiscordRateLimitException(TimeSpan retryAfter, bool isGlobal, string? scope)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"Discord API error: 429 Too Many Requests (scope={scope ?? "unknown"}, global={(isGlobal ? "true" : "false")}, retry_after={retryAfter.TotalSeconds:0.###}s)"))
    {
        RetryAfter = retryAfter;
        IsGlobal = isGlobal;
        Scope = scope;
    }

    /// <summary>Gets how long Discord asked the caller to wait before retrying.</summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>Gets a value indicating whether the global rate limit was hit.</summary>
    public bool IsGlobal { get; }

    /// <summary>Gets the <c>X-RateLimit-Scope</c> value, if Discord sent one.</summary>
    public string? Scope { get; }
}
