using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace GitHubWebhookBridge.Utils;

/// <summary>Azure Functions の <see cref="HttpResponseData"/> に <c>{ "message": ... }</c> 形式の JSON を書き込むユーティリティクラス</summary>
public static class JsonResponseHelper
{
    /// <summary>
    /// <c>{ "message": ... }</c> 形式の JSON レスポンスを生成する。
    /// <see cref="HttpResponseData.WriteAsJsonAsync{T}(T, CancellationToken)"/> は
    /// <c>WorkerOptions.Serializer</c> の DI 解決に依存するため、
    /// それを必要としない <c>WriteStringAsync</c> ベースで明示的にシリアライズする
    /// </summary>
    /// <param name="req">レスポンス生成元の HTTP リクエスト</param>
    /// <param name="statusCode">レスポンスの HTTP ステータスコード</param>
    /// <param name="message">レスポンスボディの <c>message</c> フィールドに設定する文字列</param>
    /// <returns>生成された <see cref="HttpResponseData"/></returns>
    public static async Task<HttpResponseData> CreateAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        HttpResponseData response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        var json = JsonSerializer.Serialize(new { message });
        await response.WriteStringAsync(json).ConfigureAwait(false);
        return response;
    }
}
