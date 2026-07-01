using System.Diagnostics;
using System.Net;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Tests for the retry behavior that DiscordRetryPolicy configures on the "discord" named HttpClient.
/// Reproduces the same registration used in Program.cs (AddHttpClient + AddResilienceHandler)
/// on a minimal ServiceCollection and verifies it through the actual HTTP pipeline
/// (a test that invokes only the policy internals in isolation cannot detect DI wiring mistakes).
/// </summary>
public class DiscordRetryPolicyTests
{
    private static IHttpClientFactory BuildFactory(HttpMessageHandler handler)
    {
        ServiceCollection services = new();
        services
            .AddHttpClient("discord")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler(DiscordRetryPolicy.HandlerName, DiscordRetryPolicy.Configure);
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private static HttpResponseMessage TooManyRequests(TimeSpan? retryAfter = null)
    {
        HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        if (retryAfter is { } d)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(d);
        }

        return response;
    }

    /// <summary>When a 429 occurs only once, the request succeeds after a retry.</summary>
    [Fact]
    public async Task SendAsyncRetriesOnceAndSucceedsAfterSingleTooManyRequests()
    {
        QueueHttpMessageHandler handler = new(
            _ => TooManyRequests(TimeSpan.FromMilliseconds(10)),
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.CallCount);
    }

    /// <summary>Even when 429 persists, retries stop at the maximum attempt count (initial + 2).</summary>
    [Fact]
    public async Task SendAsyncStopsAfterMaxRetryAttemptsWhenAlwaysTooManyRequests()
    {
        QueueHttpMessageHandler handler = new(_ => TooManyRequests(TimeSpan.FromMilliseconds(10)));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1 + DiscordRetryPolicy.MaxRetryAttempts, handler.CallCount);
    }

    /// <summary>Statuses other than 429 (e.g. 5xx) are not retried and the response returns immediately.</summary>
    [Fact]
    public async Task SendAsyncDoesNotRetryOnServerError()
    {
        QueueHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    /// <summary>
    /// Confirms that even for a 429 response without a Retry-After header, the DelayGenerator returns null
    /// and falls back to retries based on the default Delay/MaxDelay, succeeding after a retry
    /// (this path previously had no test, so a wiring mistake in the default backoff could not be detected).
    /// </summary>
    [Fact]
    public async Task SendAsyncRetriesUsingDefaultDelayWhenRetryAfterHeaderIsAbsent()
    {
        QueueHttpMessageHandler handler = new(
            _ => TooManyRequests(),
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.CallCount);
        // Should fit within the default Delay (500ms) plus jitter, but allow headroom in the threshold for CI flakiness
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Retry wait too long: {stopwatch.Elapsed}");
    }

    /// <summary>
    /// Confirms that even when the Retry-After header specifies an extremely long value, it is capped by MaxDelay
    /// and does not result in a retry whose total wait time is excessively long.
    /// </summary>
    [Fact]
    public async Task SendAsyncCapsWaitTimeEvenWhenRetryAfterHeaderIsVeryLong()
    {
        QueueHttpMessageHandler handler = new(
            _ => TooManyRequests(TimeSpan.FromMinutes(10)),
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Should fit within MaxDelay (2 seconds) and not wait the 10 minutes from Retry-After, but allow headroom in the threshold for CI flakiness
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Retry wait too long: {stopwatch.Elapsed}");
    }
}
