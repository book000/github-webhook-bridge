using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubWebhookBridge.Tests;

public class ConfigHttpClientConfigurationTests
{
    [Fact]
    public void ConfigClientSendsUserAgentAndUsesTenSecondTimeout()
    {
        ServiceCollection services = new();
        services.AddConfigHttpClient();
        using ServiceProvider provider = services.BuildServiceProvider();

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("config");

        Assert.Equal("github-webhook-bridge", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
    }
}
