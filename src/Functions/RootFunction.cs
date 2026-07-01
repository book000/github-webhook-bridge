using System.Net;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Functions;

/// <summary>ルートパスへの GET リクエストに応答する Azure Functions のクラス</summary>
/// <remarks>
/// <see cref="WebhookFunction"/> は POST のみを受け付けるため、GET でルートパスにアクセスすると
/// どの Function にもマッチせず、Azure Functions の既定プレースホルダーページ
/// （"Your Functions x.x app is up and running"）が表示されてしまう。
/// これを避けるため、GET 専用の Function を別途用意し、稼働確認用の簡易なレスポンスを返す
/// </remarks>
public static class RootFunction
{
    /// <summary>
    /// ルートパスへの GET リクエストを受け取り、稼働確認用のレスポンスを返す
    /// </summary>
    /// <remarks>
    /// <c>Route = ""</c>（空文字）は <c>routePrefix = ""</c> と組み合わせても関数名にフォールバックしてしまう
    /// 既知の挙動のため使用しない。<see cref="WebhookFunction"/> と同様に正規表現で空セグメントにマッチさせる
    /// </remarks>
    /// <param name="req">Azure Functions が受け取った HTTP リクエスト</param>
    /// <returns>稼働確認用のレスポンスを表す <see cref="HttpResponseData"/></returns>
    [Function("Root")]
    public static async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{x:regex(^$)?}")] HttpRequestData req)
        => await JsonResponseHelper.CreateAsync(req, HttpStatusCode.OK, "book000/github-webhook-bridge is running").ConfigureAwait(false);
}
