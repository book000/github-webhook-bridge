using System.Net;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// Azure Functions class that responds to GET requests on the root path.
/// Prevents GET requests that do not match <see cref="WebhookFunction"/> (POST only)
/// from falling through to the default Azure Functions placeholder page.
/// </summary>
public static class RootFunction
{
    /// <summary>Receives a GET request on the root path and returns a health-check response.</summary>
    /// <param name="req">The HTTP request received by Azure Functions.</param>
    /// <returns>An <see cref="HttpResponseData"/> representing the health-check response.</returns>
    [Function("Root")]
    public static async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{x:regex(^$)?}")] HttpRequestData req)
        => await JsonResponseHelper.CreateAsync(req, HttpStatusCode.OK, "book000/github-webhook-bridge is running").ConfigureAwait(false);
}
