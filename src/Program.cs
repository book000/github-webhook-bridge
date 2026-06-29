using Azure.Monitor.OpenTelemetry.Exporter;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// OpenTelemetry: Azure Functions 向け計装 + Azure Monitor エクスポーター
// AspNetCoreInstrumentation を含まない低レイヤーエクスポーターを使用し、
// Functions ホストとの二重テレメトリを防ぐ（公式推奨構成）
// APPLICATIONINSIGHTS_CONNECTION_STRING 環境変数が設定されている場合に有効
OpenTelemetryBuilder otelBuilder = builder.Services.AddOpenTelemetry();
otelBuilder.UseFunctionsWorkerDefaults();
otelBuilder.UseAzureMonitorExporter();

builder.Services
    // 汎用 HttpClient（IHttpClientFactory 経由で利用可能）
    .AddHttpClient()
    // GitHub API 用クライアント
    .AddHttpClient("github", c =>
    {
        c.BaseAddress = new Uri("https://api.github.com");
        c.DefaultRequestHeaders.Add("User-Agent", "github-webhook-bridge");
        c.Timeout = TimeSpan.FromSeconds(10);
    })
    .Services
    // 設定ファイル取得用クライアント
    .AddHttpClient("config", c => c.Timeout = TimeSpan.FromSeconds(10))
    .Services
    // Discord Webhook 用クライアント（15 秒タイムアウト）
    .AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15))
    .Services
    // Discord クライアント
    .AddSingleton<IDiscordClient, DiscordClient>()
    // MessageCacheService を具象型とインターフェースの両方で登録
    // （TableStorageInitializer が具象型を直接注入できるようにするため）
    .AddSingleton<MessageCacheService>()
    .AddSingleton<IMessageCacheService>(sp => sp.GetRequiredService<MessageCacheService>())
    // ミュートマネージャー（起動時に一度だけロード）
    .AddSingleton<IMuteManager, MuteManager>()
    // GitHub ユーザーマッピングマネージャー（起動時に一度だけロード）
    .AddSingleton<IGitHubUserMapManager, GitHubUserMapManager>()
    // CAPTIVE DEPENDENCY GUARD: ActionFactory が受け取る IServiceProvider は root SP。
    // Action の依存はすべて Singleton であること。
    // Scoped サービスを Action に追加した場合は IServiceScopeFactory を使う設計に変更すること。
    .AddSingleton<IActionFactory, ActionFactory>()
    // 起動時にアクションレジストリの DI 解決を検証
    .AddHostedService<ActionRegistryValidator>()
    // テーブルストレージの初期化をホスト起動時に非同期実行
    .AddHostedService<TableStorageInitializer>();

builder.Build().Run();
