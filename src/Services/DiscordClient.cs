using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GitHubWebhookBridge.Models.Discord;

namespace GitHubWebhookBridge.Services;

/// <summary>
/// Class implementing the Discord Webhook API client.
/// A 429 (rate limit) is not retried here; it surfaces as <see cref="DiscordRateLimitException"/>
/// so the queue worker can re-schedule the event according to Discord's Retry-After.
/// </summary>
public class DiscordClient(IHttpClientFactory httpClientFactory) : IDiscordClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <inheritdoc/>
    public async Task<string> SendMessageAsync(Uri webhookUrl, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        HttpClient http = _httpClientFactory.CreateClient("discord");
        // With ?wait=true, Discord returns the message object (including id)
        HttpResponseMessage response = await http.PostAsJsonAsync(BuildSendUrl(webhookUrl), message);
        EnsureSuccess(response);
        DiscordMessageResponse result = await response.Content.ReadFromJsonAsync<DiscordMessageResponse>()
            ?? throw new InvalidOperationException("Discord returned null message response");
        return result.Id;
    }

    /// <inheritdoc/>
    public async Task EditMessageAsync(Uri webhookUrl, string messageId, DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(webhookUrl);
        HttpClient http = _httpClientFactory.CreateClient("discord");
        Uri editUrl = BuildEditUrl(webhookUrl, messageId);
        HttpResponseMessage response = await http.PatchAsJsonAsync(editUrl, message);
        EnsureSuccess(response);
    }

    /// <summary>
    /// Safely appends ?wait=true to <paramref name="webhookUrl"/>.
    /// If query parameters already exist, joins with &amp;
    /// </summary>
    private static Uri BuildSendUrl(Uri webhookUrl)
    {
        var query = webhookUrl.Query; // "" or "?key=val"
        var suffix = query.Length == 0 ? "?wait=true" : "&wait=true";
        return new Uri($"{webhookUrl.GetLeftPart(UriPartial.Path)}{query}{suffix}");
    }

    /// <summary>
    /// Appends /messages/{messageId} to the path portion of <paramref name="webhookUrl"/> while preserving the query.
    /// Produces a correct URL even for URLs that have a query (e.g. ?thread_id=...)
    /// </summary>
    private static Uri BuildEditUrl(Uri webhookUrl, string messageId)
        => new($"{webhookUrl.GetLeftPart(UriPartial.Path)}/messages/{messageId}{webhookUrl.Query}");

    /// <summary>
    /// Performs custom error handling instead of EnsureSuccessStatusCode()
    /// to prevent the Discord Webhook token from leaking into Application Insights telemetry
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw CreateRateLimitException(response);

        throw new HttpRequestException(
            $"Discord API error: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static DiscordRateLimitException CreateRateLimitException(HttpResponseMessage response)
    {
        var isGlobal = response.Headers.TryGetValues("X-RateLimit-Global", out IEnumerable<string>? global)
            && global.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
        var scope = response.Headers.TryGetValues("X-RateLimit-Scope", out IEnumerable<string>? scopes)
            ? scopes.FirstOrDefault()
            : null;
        return new DiscordRateLimitException(ParseRetryAfter(response), isGlobal, scope);
    }

    // Discord sends Retry-After as (possibly fractional) seconds, which the typed RetryAfter header rejects.
    private static TimeSpan ParseRetryAfter(HttpResponseMessage response)
    {
        var maxSeconds = TimeSpan.FromDays(1).TotalSeconds;
        return response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds)
            && seconds > 0
            ? TimeSpan.FromSeconds(Math.Min(seconds, maxSeconds))
            : DiscordRateLimitException.DefaultRetryAfter;
    }
}
