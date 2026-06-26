using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

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
    // アクションファクトリー
    .AddSingleton<IActionFactory, ActionFactory>()
    // テーブルストレージの初期化をホスト起動時に非同期実行
    .AddHostedService<TableStorageInitializer>();

builder.Build().Run();
