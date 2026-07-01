using Azure.Monitor.OpenTelemetry.Exporter;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

// Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore (ConfigureFunctionsWebApplication) is not used
// because it has a known, unresolved bug on the Windows Consumption plan
// ("Timed out waiting for the function start call", Azure/azure-functions-dotnet-worker#3348).
// The standard ConfigureFunctionsWorkerDefaults (based on HttpRequestData/HttpResponseData) is used instead.
HostBuilder hostBuilder = new();
hostBuilder.ConfigureFunctionsWorkerDefaults();

hostBuilder.ConfigureServices(services =>
{
    // OpenTelemetry: trace collection for Azure Functions + Azure Monitor exporter.
    // Uses a low-level exporter that does not include AspNetCoreInstrumentation to
    // avoid duplicate telemetry with the Functions host (the officially recommended configuration).
    // Enabled when the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable is set.
    OpenTelemetryBuilder otelBuilder = services.AddOpenTelemetry();
    otelBuilder.UseFunctionsWorkerDefaults();
    otelBuilder.UseAzureMonitorExporter();

    services
        // General-purpose HttpClient (available via IHttpClientFactory)
        .AddHttpClient()
        // Client for the GitHub API
        .AddHttpClient("github", c =>
        {
            c.BaseAddress = new Uri("https://api.github.com");
            c.DefaultRequestHeaders.Add("User-Agent", "github-webhook-bridge");
            c.Timeout = TimeSpan.FromSeconds(10);
        })
        .Services
        // Client for fetching configuration files
        .AddHttpClient("config", c => c.Timeout = TimeSpan.FromSeconds(10))
        .Services
        // Client for Discord webhooks (15-second timeout)
        // See DiscordRetryPolicy for the retry policy details (also shared with unit tests).
        .AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15))
        .AddResilienceHandler(DiscordRetryPolicy.HandlerName, DiscordRetryPolicy.Configure)
        .Services
        // Discord client
        .AddSingleton<IDiscordClient, DiscordClient>()
        // Register MessageCacheService both as the concrete class and the interface
        // (so TableStorageInitializer can be injected with the concrete class).
        .AddSingleton<MessageCacheService>()
        .AddSingleton<IMessageCacheService>(sp => sp.GetRequiredService<MessageCacheService>())
        // Mute manager (loaded once at startup)
        .AddSingleton<IMuteManager, MuteManager>()
        // GitHub user mapping manager (loaded once at startup)
        .AddSingleton<IGitHubUserMapManager, GitHubUserMapManager>()
        // CAPTIVE DEPENDENCY GUARD: the IServiceProvider that ActionFactory receives is the root SP.
        // All Action dependencies must be Singleton.
        // If a Scoped service is added to an Action, change the design to use IServiceScopeFactory.
        // Register ActionFactory both as the concrete class and the interface
        // (so ActionRegistryValidator can be injected with the concrete ActionFactory type).
        .AddSingleton<ActionFactory>()
        .AddSingleton<IActionFactory>(sp => sp.GetRequiredService<ActionFactory>())
        // Validate DI resolution of the action registry at startup
        .AddHostedService<ActionRegistryValidator>()
        // Initialize table storage asynchronously at host startup
        .AddHostedService<TableStorageInitializer>();
});

using IHost host = hostBuilder.Build();
host.Run();
