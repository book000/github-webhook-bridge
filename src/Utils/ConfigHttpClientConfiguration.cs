using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Utils;

/// <summary>Registration of the "config" named HttpClient, shared by <see cref="Program"/> and tests.</summary>
internal static class ConfigHttpClientConfiguration
{
    /// <summary>
    /// Registers the "config" named HttpClient (used to fetch the mute rules and the user map)
    /// with a 10-second timeout and a <c>User-Agent</c> header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The builder for further configuration.</returns>
    public static IHttpClientBuilder AddConfigHttpClient(this IServiceCollection services)
        => services.AddHttpClient("config", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.Add("User-Agent", "github-webhook-bridge");
        });
}
