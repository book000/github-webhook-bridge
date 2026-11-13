using System.Net;
using GitHubWebhookBridge.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class BaseManagerResilienceTests
{
    private const string MuteUrl = "https://example.com/mutes.json";
    private const string UserMapUrl = "https://example.com/user-map.json";
    private const string ValidMuteJson = """[{"userId":1,"type":"all","events":[]}]""";
    private const string ValidUserMapJson = """{"1":"100"}""";

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static Mock<IConfiguration> CreateConfig(params (string Key, string Value)[] values)
    {
        var config = new Mock<IConfiguration>();
        foreach ((string key, string value) in values)
        {
            config.Setup(c => c[key]).Returns(value);
        }

        return config;
    }

    private static Mock<IHttpClientFactory> CreateFactory(QueueHttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("config")).Returns(() => new HttpClient(handler));
        return factory;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body),
    };

    private static MuteManager CreateMuteManager(
        QueueHttpMessageHandler handler,
        TimeProvider? clock = null,
        ILogger<MuteManager>? logger = null,
        string url = MuteUrl)
        => new(
            CreateConfig(("MUTES_FILE_URL", url)).Object,
            CreateFactory(handler).Object,
            clock,
            logger);

    private static GitHubUserMapManager CreateUserMapManager(
        QueueHttpMessageHandler handler,
        TimeProvider? clock = null)
        => new(
            CreateConfig(("GITHUB_USER_MAP_FILE_URL", UserMapUrl)).Object,
            CreateFactory(handler).Object,
            clock);

    private static AdjustableTimeProvider NewClock()
        => new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task MuteManagerTooManyRequestsDegradesToNotMuted()
    {
        var handler = new QueueHttpMessageHandler(_ => Status(HttpStatusCode.TooManyRequests));
        MuteManager mgr = CreateMuteManager(handler);

        await mgr.EnsureLoadedAsync();

        Assert.False(mgr.IsMuted(1, "push", null));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UserMapManagerServerErrorDegradesToNoMapping()
    {
        var handler = new QueueHttpMessageHandler(_ => Status(HttpStatusCode.InternalServerError));
        GitHubUserMapManager mgr = CreateUserMapManager(handler);

        await mgr.EnsureLoadedAsync();

        Assert.Null(mgr.GetById(1));
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("socket")]
    [InlineData("malformed")]
    public async Task TransientRetrievalFailuresDegradeToNotMuted(string kind)
    {
        Func<HttpRequestMessage, HttpResponseMessage> responder = kind switch
        {
            "timeout" => _ => throw new TaskCanceledException("timed out"),
            "socket" => _ => throw new HttpRequestException("connection reset"),
            "malformed" => _ => Json("{not json"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown failure kind"),
        };
        MuteManager mgr = CreateMuteManager(new QueueHttpMessageHandler(responder));

        await mgr.EnsureLoadedAsync();

        Assert.False(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task FileSourceIoExceptionDegradesToNotMuted()
    {
        string parentFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfig(("MUTES_FILE_PATH", Path.Combine(parentFile, "mutes.json")));
            var mgr = new MuteManager(config.Object, new Mock<IHttpClientFactory>().Object);

            await mgr.EnsureLoadedAsync();

            Assert.False(mgr.IsMuted(1, "push", null));
        }
        finally
        {
            File.Delete(parentFile);
        }
    }

    [Fact]
    public async Task CooldownSuppressesRetriesUntilItExpires()
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new QueueHttpMessageHandler(_ => Status(HttpStatusCode.TooManyRequests));
        MuteManager mgr = CreateMuteManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        Assert.Equal(1, handler.CallCount);

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        await mgr.EnsureLoadedAsync();
        Assert.Equal(1, handler.CallCount);

        clock.Advance(TimeSpan.FromSeconds(2));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SuccessfulRetryAfterFailureLoadsDataAndWaitsForTheNextInterval()
    {
        AdjustableTimeProvider clock = NewClock();
        var handler = new QueueHttpMessageHandler(
            _ => Status(HttpStatusCode.InternalServerError),
            _ => Json(ValidMuteJson));
        MuteManager mgr = CreateMuteManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        Assert.False(mgr.IsMuted(1, "push", null));

        clock.Advance(TimeSpan.FromMinutes(5));
        await mgr.EnsureLoadedAsync();
        Assert.True(mgr.IsMuted(1, "push", null));

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        await mgr.EnsureLoadedAsync();
        Assert.Equal(2, handler.CallCount);
        Assert.True(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task ReloadAfterTheRefreshIntervalReplacesTheData()
    {
        AdjustableTimeProvider clock = NewClock();
        var handler = new QueueHttpMessageHandler(_ => Json(ValidMuteJson), _ => Json("[]"));
        MuteManager mgr = CreateMuteManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        Assert.True(mgr.IsMuted(1, "push", null));

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(1, handler.CallCount);
        Assert.True(mgr.IsMuted(1, "push", null));

        clock.Advance(TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(2, handler.CallCount);
        Assert.False(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task FailedReloadKeepsThePreviousDataAndWaitsForTheNextInterval()
    {
        AdjustableTimeProvider clock = NewClock();
        var logger = new CapturingLogger<MuteManager>();
        var handler = new QueueHttpMessageHandler(
            _ => Json(ValidMuteJson),
            _ => Status(HttpStatusCode.InternalServerError),
            _ => Json("[]"));
        MuteManager mgr = CreateMuteManager(handler, clock, logger);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(5));
        await mgr.EnsureLoadedAsync();

        Assert.Equal(2, handler.CallCount);
        Assert.True(mgr.IsMuted(1, "push", null));
        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("keeping the previously loaded data", message, StringComparison.Ordinal);

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(2, handler.CallCount);
        Assert.True(mgr.IsMuted(1, "push", null));

        clock.Advance(TimeSpan.FromSeconds(1));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(3, handler.CallCount);
        Assert.False(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task NullContentOnTheFirstLoadDegradesToNotMuted()
    {
        var handler = new QueueHttpMessageHandler(_ => Json("null"));
        MuteManager mgr = CreateMuteManager(handler);

        await mgr.EnsureLoadedAsync();

        Assert.False(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task NullContentOnAReloadKeepsThePreviousData()
    {
        AdjustableTimeProvider clock = NewClock();
        var handler = new QueueHttpMessageHandler(_ => Json(ValidMuteJson), _ => Json("null"));
        MuteManager mgr = CreateMuteManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(5));
        await mgr.EnsureLoadedAsync();

        Assert.Equal(2, handler.CallCount);
        Assert.True(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task UserMapReloadReplacesTheMappingAndFailedReloadKeepsIt()
    {
        AdjustableTimeProvider clock = NewClock();
        var handler = new QueueHttpMessageHandler(
            _ => Json(ValidUserMapJson),
            _ => Json("""{"1":"200"}"""),
            _ => Status(HttpStatusCode.InternalServerError));
        GitHubUserMapManager mgr = CreateUserMapManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        Assert.Equal("100", mgr.GetById(1));

        clock.Advance(TimeSpan.FromMinutes(5));
        await mgr.EnsureLoadedAsync();
        Assert.Equal("200", mgr.GetById(1));

        clock.Advance(TimeSpan.FromMinutes(5));
        await mgr.EnsureLoadedAsync();
        Assert.Equal(3, handler.CallCount);
        Assert.Equal("200", mgr.GetById(1));
    }

    [Fact]
    public async Task ConcurrentCallDuringAReloadServesThePreviousDataWithoutWaiting()
    {
        AdjustableTimeProvider clock = NewClock();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var handler = new QueueHttpMessageHandler(
            _ => Json(ValidMuteJson),
            _ =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                return Json("[]");
            });
        MuteManager mgr = CreateMuteManager(handler, clock);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(5));
        Task reload = Task.Run(mgr.EnsureLoadedAsync);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        await mgr.EnsureLoadedAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(reload.IsCompleted);
        Assert.True(mgr.IsMuted(1, "push", null));

        release.Set();
        await reload;
        Assert.Equal(2, handler.CallCount);
        Assert.False(mgr.IsMuted(1, "push", null));
    }

    [Fact]
    public async Task UserMapSuccessfulLoadIsUsedWithoutDegrading()
    {
        var handler = new QueueHttpMessageHandler(_ => Json(ValidUserMapJson));
        GitHubUserMapManager mgr = CreateUserMapManager(handler);

        await mgr.EnsureLoadedAsync();

        Assert.Equal("100", mgr.GetById(1));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task NonHttpsUrlStillThrowsWithoutSendingRequests()
    {
        var handler = new QueueHttpMessageHandler(_ => Json(ValidMuteJson));
        MuteManager mgr = CreateMuteManager(handler, url: "http://example.com/mutes.json");

        await Assert.ThrowsAsync<InvalidOperationException>(mgr.EnsureLoadedAsync);

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task BlobPathWithoutContainerStillThrows()
    {
        var mgr = new MuteManager(
            CreateConfig(("MUTES_BLOB", "noslash")).Object,
            new Mock<IHttpClientFactory>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(mgr.EnsureLoadedAsync);
    }

    [Fact]
    public async Task BlobSourceWithoutStorageConnectionStringStillThrows()
    {
        var mgr = new MuteManager(
            CreateConfig(("MUTES_BLOB", "container/file.json")).Object,
            new Mock<IHttpClientFactory>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(mgr.EnsureLoadedAsync);
    }

    [Fact]
    public async Task DegradedLoadLogsOneWarningWithoutUrlOrExceptionMessage()
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var logger = new CapturingLogger<MuteManager>();
        var handler = new QueueHttpMessageHandler(
            _ => throw new HttpRequestException("sentinel-exception-message"));
        MuteManager mgr = CreateMuteManager(
            handler,
            clock,
            logger,
            "https://example.com/sentinel-url-segment/mutes.json");

        await mgr.EnsureLoadedAsync();
        await mgr.EnsureLoadedAsync();

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("MuteManager", message, StringComparison.Ordinal);
        Assert.Contains("url", message, StringComparison.Ordinal);
        Assert.Contains("HttpRequestException", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-url-segment", message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-exception-message", message, StringComparison.Ordinal);
    }
}
