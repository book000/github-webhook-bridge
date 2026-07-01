using System.Collections.Specialized;
using System.Net;
using System.Text;
using GitHubWebhookBridge.Functions;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for RootFunction.RunAsync().</summary>
public class RootFunctionTests
{
    /// <summary>Reads the response body as a UTF-8 string.</summary>
    private static string ReadBody(HttpResponseData response)
    {
        var stream = (MemoryStream)response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A GET request returns 200 OK with a health-check message.</summary>
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
