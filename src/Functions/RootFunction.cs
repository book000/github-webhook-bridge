using System.Net;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Functions;

/// <summary>
/// ルートパスへの GET リクエストに応答する Azure Functions のクラス。
/// <see cref="WebhookFunction"/>（POST 専用）にマッチしない GET リクエストが
/// Azure Functions の既定プレースホルダーページに落ちるのを防ぐ
/// </summary>
public static class RootFunction
{
    /// <summary>ルートパスへの GET リクエストを受け取り、稼働確認用のレスポンスを返す</summary>
    /// <param name="req">Azure Functions が受け取った HTTP リクエスト</param>
    /// <returns>稼働確認用のレスポンスを表す <see cref="HttpResponseData"/></returns>
    [Function("Root")]
    public static async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{x:regex(^$)?}")] HttpRequestData req)
        => await JsonResponseHelper.CreateAsync(req, HttpStatusCode.OK, "book000/github-webhook-bridge is running").ConfigureAwait(false);
}
