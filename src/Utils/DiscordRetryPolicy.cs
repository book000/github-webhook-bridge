using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// Defines the retry policy applied to the "discord" named HttpClient.
/// Targets only 429 (rate limit), respecting the Retry-After header while capping at a short limit,
/// to prevent the entire Webhook processing from being blocked for a long time during Discord-side failures or congestion.
/// Referenced from both <see cref="Program"/> and unit tests to consolidate the implementation in one place
/// </summary>
public static class DiscordRetryPolicy
{
    /// <summary>Handler name passed to AddResilienceHandler.</summary>
    public const string HandlerName = "discord-retry";

    /// <summary>Maximum number of retries (not including the initial attempt).</summary>
    public const int MaxRetryAttempts = 2;

    /// <summary>Base wait time used when there is no Retry-After header.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    /// <summary>Upper bound on the wait time, including the value specified by the Retry-After header.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Adds a retry strategy to <see cref="ResiliencePipelineBuilder{HttpResponseMessage}"/>
    /// </summary>
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        HttpRetryStrategyOptions options = new()
        {
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests),
            MaxRetryAttempts = MaxRetryAttempts,
            BackoffType = DelayBackoffType.Constant,
            Delay = Delay,
            MaxDelay = MaxDelay,
            UseJitter = true,
            // The default ShouldRetryAfterHeader = true adopts the Retry-After value as-is and does not
            // cap it at MaxDelay (confirmed in v10.7.0), so disable it and round via the DelayGenerator below
            ShouldRetryAfterHeader = false,
            DelayGenerator = args =>
            {
                RetryConditionHeaderValue? retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                TimeSpan? delay = retryAfter switch
                {
                    { Delta: { } d } => d,
                    { Date: { } date } => date - DateTimeOffset.UtcNow,
                    _ => null,
                };
                return ValueTask.FromResult(delay is { } d2 && d2 > TimeSpan.Zero
                    ? (TimeSpan?)(d2 > MaxDelay ? MaxDelay : d2)
                    : null);
            },
        };

        builder.AddRetry(options);
    }
}
