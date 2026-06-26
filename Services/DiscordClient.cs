using System.Net.Http.Json;
using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>Discord Webhook API クライアント実装。</summary>
public class DiscordClient : IDiscordClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DiscordClient(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public async Task<string> SendMessageAsync(string webhookUrl, DiscordMessage message)
    {
        var http = _httpClientFactory.CreateClient("discord");
        // ?wait=true で Discord がメッセージオブジェクト (id 含む) を返す
        var response = await http.PostAsJsonAsync(webhookUrl + "?wait=true", message);
        EnsureSuccess(response);
        var result = await response.Content.ReadFromJsonAsync<DiscordMessageResponse>()
            ?? throw new InvalidOperationException("Discord returned null message response");
        return result.Id;
    }

    public async Task EditMessageAsync(string webhookUrl, string messageId, DiscordMessage message)
    {
        var http     = _httpClientFactory.CreateClient("discord");
        var editUrl  = $"{webhookUrl}/messages/{messageId}";
        var response = await http.PatchAsJsonAsync(editUrl, message);
        EnsureSuccess(response);
    }

    /// <summary>
    /// Discord Webhook トークンが Application Insights テレメトリに漏洩しないよう、
    /// EnsureSuccessStatusCode() の代わりに独自エラー処理を行う。
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Discord API error: {(int)response.StatusCode} {response.ReasonPhrase}");
    }
}
