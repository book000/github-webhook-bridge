using System.Collections.Specialized;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Both <see cref="HttpRequestData"/> and <see cref="HttpResponseData"/> are abstract classes;
/// this is a minimal, test-only implementation for instantiating them outside the Functions runtime.
/// Provided in-house because no official test helper exists.
/// </summary>
internal sealed class FakeFunctionContext : FunctionContext
{
    public override string InvocationId { get; } = Guid.NewGuid().ToString();
    public override string FunctionId { get; } = "GitHubWebhook";
    public override TraceContext TraceContext { get; } = null!;
    public override BindingContext BindingContext { get; } = null!;
    public override RetryContext RetryContext { get; } = null!;
    public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
    public override FunctionDefinition FunctionDefinition { get; } = null!;
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
    public override IInvocationFeatures Features { get; } = null!;
}

internal sealed class FakeHttpRequestData(
    FunctionContext functionContext,
    Stream body,
    HttpHeadersCollection headers,
    NameValueCollection query,
    string method = "POST")
    : HttpRequestData(functionContext)
{
    public override Stream Body { get; } = body;
    public override HttpHeadersCollection Headers { get; } = headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];
    public override Uri Url { get; } = new("https://gwb.tomacheese.com/");
    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];
    public override string Method { get; } = method;
    public override NameValueCollection Query { get; } = query;

    public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);
}

internal sealed class FakeHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
{
    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; } = [];
    public override Stream Body { get; set; } = new MemoryStream();
    public override HttpCookies Cookies { get; } = null!;
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that returns pre-configured responses in order.
/// Used to verify how many requests the retry handler actually sent.
/// Once the configured responses are exhausted, it keeps returning the last one.
/// </summary>
internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>[] _responders;
    private int _callCount;

    public QueueHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
    {
        ArgumentNullException.ThrowIfNull(responders);
        if (responders.Length == 0)
        {
            throw new ArgumentException("At least one responder must be provided.", nameof(responders));
        }

        _responders = responders;
    }

    /// <summary>The number of requests actually sent.</summary>
    public int CallCount => _callCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int index = Interlocked.Increment(ref _callCount) - 1;
        Func<HttpRequestMessage, HttpResponseMessage> responder = _responders[Math.Min(index, _responders.Length - 1)];
        return Task.FromResult(responder(request));
    }
}
