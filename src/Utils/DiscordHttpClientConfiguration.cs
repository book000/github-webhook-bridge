using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Utils;

/// <summary>Registration of the "discord" named HttpClient, shared by <see cref="Program"/> and tests.</summary>
internal static class DiscordHttpClientConfiguration
{
    /// <summary>
    /// Registers the "discord" named HttpClient with a 15-second timeout.
    /// The default HttpClientFactory request logging is removed because it logs the full request URI,
    /// which contains the webhook token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The builder for further configuration.</returns>
    public static IHttpClientBuilder AddDiscordHttpClient(this IServiceCollection services)
        => services.AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
}
