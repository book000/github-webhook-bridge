using Azure.Monitor.OpenTelemetry.Exporter;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

// Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore（ConfigureFunctionsWebApplication）は
// Windows Consumption プランで「Timed out waiting for the function start call」という
// 既知の未解決バグ（Azure/azure-functions-dotnet-worker#3348）を抱えているため使用しない。
// 標準の ConfigureFunctionsWorkerDefaults（HttpRequestData/HttpResponseData ベース）を使用する
HostBuilder hostBuilder = new();
hostBuilder.ConfigureFunctionsWorkerDefaults();

hostBuilder.ConfigureServices(services =>
{
    // OpenTelemetry: Azure Functions 向けトレース収集 + Azure Monitor エクスポーター
    // AspNetCoreInstrumentation を含まない低レイヤーエクスポーターを使用し、
    // Functions ホストとの二重テレメトリを防ぐ（公式推奨構成）
    // APPLICATIONINSIGHTS_CONNECTION_STRING 環境変数が設定されている場合に有効
    OpenTelemetryBuilder otelBuilder = services.AddOpenTelemetry();
    otelBuilder.UseFunctionsWorkerDefaults();
    otelBuilder.UseAzureMonitorExporter();

    services
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
        // 再試行ポリシーの内容は DiscordRetryPolicy を参照（単体テストとも共有）
        .AddHttpClient("discord", c => c.Timeout = TimeSpan.FromSeconds(15))
        .AddResilienceHandler(DiscordRetryPolicy.HandlerName, DiscordRetryPolicy.Configure)
        .Services
        // Discord クライアント
        .AddSingleton<IDiscordClient, DiscordClient>()
        // MessageCacheService をクラス直接・インターフェースの両方で登録
        // （TableStorageInitializer がクラス直接で注入できるようにするため）
        .AddSingleton<MessageCacheService>()
        .AddSingleton<IMessageCacheService>(sp => sp.GetRequiredService<MessageCacheService>())
        // ミュートマネージャー（起動時に一度だけロード）
        .AddSingleton<IMuteManager, MuteManager>()
        // GitHub ユーザーマッピングマネージャー（起動時に一度だけロード）
        .AddSingleton<IGitHubUserMapManager, GitHubUserMapManager>()
        // CAPTIVE DEPENDENCY GUARD: ActionFactory が受け取る IServiceProvider は root SP。
        // Action の依存はすべて Singleton であること。
        // Scoped サービスを Action に追加した場合は IServiceScopeFactory を使う設計に変更すること。
        // ActionFactory をクラス直接・インターフェースの両方で登録
        // （ActionRegistryValidator が具象型 ActionFactory を直接注入できるようにするため）
        .AddSingleton<ActionFactory>()
        .AddSingleton<IActionFactory>(sp => sp.GetRequiredService<ActionFactory>())
        // 起動時にアクションレジストリの DI 解決を検証
        .AddHostedService<ActionRegistryValidator>()
        // テーブルストレージの初期化をホスト起動時に非同期実行
        .AddHostedService<TableStorageInitializer>();
});

using IHost host = hostBuilder.Build();
host.Run();
