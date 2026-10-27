using System.Net;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="DiscordClient"/> rate-limit handling and for keeping the webhook token out of logs.</summary>
public class DiscordClientTests
{
    private const string Token = "SECRET-TOKEN-abc123";
    private static readonly Uri _webhookUrl = new($"https://discord.com/api/webhooks/123456/{Token}");
    private static readonly DiscordMessage _message = new("hello");

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add(formatter(state, exception) + exception);
                }
            }
        }
    }

    private static DiscordClient BuildClient(HttpMessageHandler handler, CapturingLoggerProvider? logs = null, bool useSharedConfiguration = true)
    {
        ServiceCollection services = new();
        if (logs is not null)
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));

        IHttpClientBuilder builder = useSharedConfiguration
            ? services.AddDiscordHttpClient()
            : services.AddHttpClient("discord");
        builder.ConfigurePrimaryHttpMessageHandler(() => handler);

        return new DiscordClient(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
    }

    private static HttpResponseMessage TooManyRequests(string? retryAfter = null, string? global = null, string? scope = null)
    {
        HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        if (retryAfter is not null)
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        if (global is not null)
            response.Headers.TryAddWithoutValidation("X-RateLimit-Global", global);
        if (scope is not null)
            response.Headers.TryAddWithoutValidation("X-RateLimit-Scope", scope);
        return response;
    }

    private static Task<DiscordRateLimitException> SendExpectingRateLimit(HttpResponseMessage response)
    {
        DiscordClient client = BuildClient(new QueueHttpMessageHandler(_ => response));
        return Assert.ThrowsAsync<DiscordRateLimitException>(() => client.SendMessageAsync(_webhookUrl, _message));
    }

    [Fact]
    public async Task SendMessageAsync_429WithRetryAfter_ThrowsWithThatDelay()
    {
        DiscordRateLimitException ex = await SendExpectingRateLimit(TooManyRequests("7", scope: "user"));

        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
        Assert.False(ex.IsGlobal);
        Assert.Equal("user", ex.Scope);
        Assert.Equal("Discord API error: 429 Too Many Requests (scope=user, global=false, retry_after=7s)", ex.Message);
    }

    [Fact]
    public async Task SendMessageAsync_429WithFractionalRetryAfterAndGlobal_ParsesBoth()
    {
        DiscordRateLimitException ex = await SendExpectingRateLimit(TooManyRequests("2.5", global: "true", scope: "global"));

        Assert.Equal(TimeSpan.FromSeconds(2.5), ex.RetryAfter);
        Assert.True(ex.IsGlobal);
        Assert.Equal("Discord API error: 429 Too Many Requests (scope=global, global=true, retry_after=2.5s)", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task SendMessageAsync_429WithoutUsableRetryAfter_FallsBackTo30Seconds(string? header)
    {
        DiscordRateLimitException ex = await SendExpectingRateLimit(TooManyRequests(header));

        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
        Assert.Equal("Discord API error: 429 Too Many Requests (scope=unknown, global=false, retry_after=30s)", ex.Message);
    }

    [Fact]
    public async Task SendMessageAsync_429WithHugeRetryAfter_IsCappedAtOneDay()
    {
        DiscordRateLimitException ex = await SendExpectingRateLimit(TooManyRequests("1e30"));

        Assert.Equal(TimeSpan.FromDays(1), ex.RetryAfter);
    }

    [Fact]
    public async Task EditMessageAsync_429_ThrowsRateLimitException()
    {
        DiscordClient client = BuildClient(new QueueHttpMessageHandler(_ => TooManyRequests("4")));

        DiscordRateLimitException ex = await Assert.ThrowsAsync<DiscordRateLimitException>(
            () => client.EditMessageAsync(_webhookUrl, "999", _message));

        Assert.Equal(TimeSpan.FromSeconds(4), ex.RetryAfter);
    }

    [Fact]
    public async Task SendMessageAsync_429_IsNotRetriedInProcess()
    {
        QueueHttpMessageHandler handler = new(_ => TooManyRequests("1"));
        DiscordClient client = BuildClient(handler);

        await Assert.ThrowsAsync<DiscordRateLimitException>(() => client.SendMessageAsync(_webhookUrl, _message));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendMessageAsync_500_ThrowsPlainHttpRequestExceptionWithoutUrl()
    {
        DiscordClient client = BuildClient(new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMessageAsync(_webhookUrl, _message));

        Assert.IsNotType<DiscordRateLimitException>(ex);
        Assert.Equal("Discord API error: 500 Internal Server Error", ex.Message);
    }

    [Fact]
    public async Task Sending_ThroughSharedHttpClientConfiguration_NeverLogsTheWebhookToken()
    {
        CapturingLoggerProvider logs = new();
        DiscordClient client = BuildClient(
            new QueueHttpMessageHandler(_ => TooManyRequests("1"), _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            logs);

        await Assert.ThrowsAsync<DiscordRateLimitException>(() => client.SendMessageAsync(_webhookUrl, _message));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMessageAsync(_webhookUrl, _message));

        Assert.DoesNotContain(logs.Entries, e => e.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sending_WithoutSharedConfiguration_LogsTheWebhookToken()
    {
        CapturingLoggerProvider logs = new();
        DiscordClient client = BuildClient(
            new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            logs,
            useSharedConfiguration: false);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMessageAsync(_webhookUrl, _message));

        Assert.Contains(logs.Entries, e => e.Contains(Token, StringComparison.Ordinal));
    }
}
