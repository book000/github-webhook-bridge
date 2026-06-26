using System.Net.Http.Json;
using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>Discord Webhook API クライアント実装。</summary>
public class DiscordClient(IHttpClientFactory httpClientFactory) : IDiscordClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<string> SendMessageAsync(Uri webhookUrl, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        HttpClient http = _httpClientFactory.CreateClient("discord");
        // ?wait=true で Discord がメッセージオブジェクト (id 含む) を返す
        HttpResponseMessage response = await http.PostAsJsonAsync(BuildSendUrl(webhookUrl), message);
        EnsureSuccess(response);
        DiscordMessageResponse result = await response.Content.ReadFromJsonAsync<DiscordMessageResponse>()
            ?? throw new InvalidOperationException("Discord returned null message response");
        return result.Id;
    }

    public async Task EditMessageAsync(Uri webhookUrl, string messageId, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        HttpClient http = _httpClientFactory.CreateClient("discord");
        Uri editUrl = BuildEditUrl(webhookUrl, messageId);
        HttpResponseMessage response = await http.PatchAsJsonAsync(editUrl, message);
        EnsureSuccess(response);
    }

    /// <summary>
    /// webhookUrl に ?wait=true を安全に付加する。
    /// 既にクエリパラメータが存在する場合は &amp; で連結する。
    /// </summary>
    private static Uri BuildSendUrl(Uri webhookUrl)
    {
        var query = webhookUrl.Query; // "" または "?key=val"
        var suffix = query.Length == 0 ? "?wait=true" : "&wait=true";
        return new Uri($"{webhookUrl.GetLeftPart(UriPartial.Path)}{query}{suffix}");
    }

    /// <summary>
    /// webhookUrl のパス部分に /messages/{messageId} を付加し、クエリを保持する。
    /// クエリがある URL（例: ?thread_id=...）に対しても正しい URL を生成する。
    /// </summary>
    private static Uri BuildEditUrl(Uri webhookUrl, string messageId)
        => new($"{webhookUrl.GetLeftPart(UriPartial.Path)}/messages/{messageId}{webhookUrl.Query}");

    /// <summary>
    /// Discord Webhook トークンが Application Insights テレメトリに漏洩しないよう、
    /// EnsureSuccessStatusCode() の代わりに独自エラー処理を行う。
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Discord API error: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }
}
