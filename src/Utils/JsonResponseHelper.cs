using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Utils;

/// <summary>Utility class that writes <c>{ "message": ... }</c> style JSON to an Azure Functions <see cref="HttpResponseData"/></summary>
public static class JsonResponseHelper
{
    /// <summary>
    /// Builds a <c>{ "message": ... }</c> style JSON response.
    /// <c>HttpResponseData.WriteAsJsonAsync</c> depends on
    /// DI resolution of <c>WorkerOptions.Serializer</c>,
    /// so this serializes explicitly using <c>WriteStringAsync</c>, which does not require it
    /// </summary>
    /// <param name="req">HTTP request from which the response is created</param>
    /// <param name="statusCode">HTTP status code of the response</param>
    /// <param name="message">String set in the <c>message</c> field of the response body</param>
    /// <returns>The created <see cref="HttpResponseData"/></returns>
    public static async Task<HttpResponseData> CreateAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        HttpResponseData response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        var json = JsonSerializer.Serialize(new { message });
        await response.WriteStringAsync(json).ConfigureAwait(false);
        return response;
    }
}
