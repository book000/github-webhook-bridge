using System.Collections.Specialized;
using System.Net;
using System.Text;
using GitHubWebhookBridge.Functions;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Tests;

/// <summary>RootFunction.RunAsync() のテスト。</summary>
public class RootFunctionTests
{
    /// <summary>レスポンスボディを UTF-8 文字列として読み出す。</summary>
    private static string ReadBody(HttpResponseData response)
    {
        var stream = (MemoryStream)response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>GET リクエストは 200 OK と稼働確認メッセージを返す。</summary>
    [Fact]
    public async Task RunGetRequestReturns200WithMessage()
    {
        var context = new FakeFunctionContext();
        HttpRequestData req = new FakeHttpRequestData(context, new MemoryStream(), [], new NameValueCollection(), "GET");

        HttpResponseData result = await RootFunction.RunAsync(req);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("book000/github-webhook-bridge is running", ReadBody(result), StringComparison.Ordinal);
    }
}
