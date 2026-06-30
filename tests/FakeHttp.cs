using System.Collections.Specialized;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// <see cref="HttpRequestData"/> / <see cref="HttpResponseData"/> はいずれも抽象クラスであり、
/// Functions ランタイム外でインスタンス化するためのテスト専用最小実装。
/// 公式テストヘルパーが提供されていないため自前で用意する
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
    NameValueCollection query)
    : HttpRequestData(functionContext)
{
    public override Stream Body { get; } = body;
    public override HttpHeadersCollection Headers { get; } = headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];
    public override Uri Url { get; } = new("https://gwb.tomacheese.com/");
    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];
    public override string Method { get; } = "POST";
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
