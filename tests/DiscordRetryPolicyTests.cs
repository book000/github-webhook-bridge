using System.Diagnostics;
using System.Net;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// DiscordRetryPolicy が "discord" 名前付き HttpClient に構成する再試行挙動のテスト。
/// Program.cs と同一の登録方法（AddHttpClient + AddResilienceHandler）を
/// 最小構成の ServiceCollection で再現し、実際の HTTP パイプラインを通して検証する
/// （ポリシーの中身だけを単体で呼び出すテストでは、DI 登録の配線ミスを検出できないため）
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

    /// <summary>429 が 1 回だけ発生した場合、再試行の末に成功する。</summary>
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

    /// <summary>429 が続く場合でも、最大試行回数（初回 + 2 回）で打ち切られる。</summary>
    [Fact]
    public async Task SendAsyncStopsAfterMaxRetryAttemptsWhenAlwaysTooManyRequests()
    {
        QueueHttpMessageHandler handler = new(_ => TooManyRequests(TimeSpan.FromMilliseconds(10)));
        HttpClient client = BuildFactory(handler).CreateClient("discord");

        HttpResponseMessage response = await client.GetAsync(new Uri("https://discord.test/webhook"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1 + DiscordRetryPolicy.MaxRetryAttempts, handler.CallCount);
    }

    /// <summary>429 以外（5xx 等）は再試行の対象外で、即座に応答が返る。</summary>
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
    /// Retry-After ヘッダーが無い 429 応答でも、DelayGenerator が null を返して
    /// 既定の Delay/MaxDelay に基づく再試行にフォールバックし、再試行の末に成功することを確認する
    /// （このパスはこれまでテストが無く、既定バックオフの配線ミスを検出できなかったための追加）。
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
        // 既定の Delay（500ms）+ ジッター程度に収まるはずだが、CI のフレーク耐性のため閾値には余裕を持たせる
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Retry wait too long: {stopwatch.Elapsed}");
    }

    /// <summary>
    /// Retry-After ヘッダーが極端に長い値を指定してきても、MaxDelay で頭打ちになり、
    /// 全体の待機時間が長すぎるリトライにならないことを確認する
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
        // MaxDelay（2 秒）に収まるはずで Retry-After の 10 分は待たないが、CI のフレーク耐性のため閾値には余裕を持たせる
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Retry wait too long: {stopwatch.Elapsed}");
    }
}
