using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace GitHubWebhookBridge.Functions;

/// <summary>GitHub Webhook を受信し Discord に通知する Azure Function。</summary>
/// <remarks>依存サービスをコンストラクタインジェクションで受け取る。</remarks>
public class WebhookFunction(
    IActionFactory actionFactory,
    IMuteManager muteManager,
    IConfiguration config,
    ILogger<WebhookFunction> logger)
{
    private readonly IActionFactory _actionFactory = actionFactory;
    private readonly IMuteManager _muteManager = muteManager;
    private readonly IConfiguration _config = config;
    private readonly ILogger<WebhookFunction> _logger = logger;

    // JSON デシリアライズオプション（キャッシュして再利用）
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// GitHub Webhook リクエストを受け取り、署名検証・ミュートチェックを経て Discord に通知する。
    /// </summary>
    /// <param name="req">Azure Functions が受け取った HTTP リクエスト。</param>
    /// <returns>処理結果を表す <see cref="IActionResult"/>。</returns>
    [Function("GitHubWebhook")]
    [SuppressMessage("Naming", "IDE1006:NamingRuleViolation", Justification = "Azure Functions ランタイムが 'Run' という名前を要求するため変更不可")]
    [SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Webhook エントリーポイントは必然的に多くの型を参照する")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "GitHubWebhook")] HttpRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // リクエストボディの上限サイズ (10 MB)
        const long MaxBodyBytes = 10L * 1024 * 1024;

        // 1. Content-Length 事前チェック（10MB 超過は Bad Request）
        if (req.ContentLength.HasValue && req.ContentLength.Value > MaxBodyBytes)
            return new BadRequestObjectResult(new { message = "Bad Request: Body too large" });

        // 2. ボディを MaxBodyBytes まで逐次読み取り
        req.EnableBuffering();
        using var ms = new MemoryStream();
        var chunk = new byte[81920];
        int bytesRead;
        while ((bytesRead = await req.Body.ReadAsync(chunk)) > 0)
        {
            await ms.WriteAsync(chunk.AsMemory(0, bytesRead));
            if (ms.Length > MaxBodyBytes)
                return new BadRequestObjectResult(new { message = "Bad Request: Body too large" });
        }

        var rawBody = ms.ToArray();

        if (rawBody.Length == 0)
            return new BadRequestObjectResult(new { message = "Bad Request: Empty body" });

        req.Body.Position = 0;

        // 3. HMAC-SHA256 署名検証
        var secret = _config["GITHUB_WEBHOOK_SECRET"]
            ?? throw new InvalidOperationException("GITHUB_WEBHOOK_SECRET not set");
        if (!SignatureValidator.Validate(rawBody, req.Headers, secret))
            return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-Hub-Signature-256" });

        // 4. X-GitHub-Event ヘッダー検証（ログインジェクション防止のためサニタイズ）
        var rawEventName = req.Headers["X-GitHub-Event"].ToString();
        if (string.IsNullOrEmpty(rawEventName))
            return new BadRequestObjectResult(new { message = "Bad Request: Missing X-GitHub-Event" });

        var sanitizedEventName = SanitizeEventName(rawEventName);
        if (sanitizedEventName != rawEventName)
        {
            _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
            return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-GitHub-Event" });
        }

        // ActionFactory の switch 式は小文字前提のため小文字に正規化する
        var eventName = NormalizeEventName(rawEventName);

        // 5. ?url= — discord.com Webhook URL に限定（SSRF 対策）
        Uri webhookUrl;
        if (req.Query.TryGetValue("url", out StringValues urlParam) && !string.IsNullOrEmpty(urlParam))
        {
            var candidate = urlParam.ToString();
            if (!IsAllowedWebhookUrl(candidate))
                return new BadRequestObjectResult(new { message = "Bad Request: Invalid url parameter" });
            webhookUrl = new Uri(candidate);
        }
        else
        {
            // ?url= が省略された場合は環境変数のデフォルト Webhook URL を使用
            var defaultUrl = _config["DISCORD_WEBHOOK_URL"]
                ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set");
            webhookUrl = new Uri(defaultUrl);
        }

        // 6. ?disabled-events= チェック（カンマ区切り、イベント名が含まれる場合は 202 を返す）
        var disabledEvents = req.Query.TryGetValue("disabled-events", out StringValues disabledEventsParam)
                             && !string.IsNullOrEmpty(disabledEventsParam)
            ? disabledEventsParam.ToString()
            : _config["DISABLED_EVENTS"];
        if (!string.IsNullOrEmpty(disabledEvents))
        {
            var disabledEventNames = disabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (disabledEventNames.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                return new ObjectResult(new { message = "Disabled event" }) { StatusCode = 202 };
        }

        // 7. JSON デシリアライズ（不正 JSON は 400 を返す）
        JsonElement body;
        try
        {
            body = JsonSerializer.Deserialize<JsonElement>(rawBody, _jsonOptions);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { message = "Bad Request: Invalid JSON body" });
        }

        // 8. 送信者ミュートチェック
        await _muteManager.EnsureLoadedAsync();
        if (body.TryGetProperty("sender", out JsonElement sender)
            && sender.TryGetProperty("id", out JsonElement senderId)
            && senderId.ValueKind == JsonValueKind.Number)
        {
            var actionProp = body.TryGetProperty("action", out JsonElement actionElement) ? actionElement.GetString() : null;
            if (_muteManager.IsMuted(senderId.GetInt64(), eventName, actionProp))
                return new OkObjectResult(new { message = "Muted user" });
        }

        // 9-10. アクションハンドラーへディスパッチ
        IAction? actionHandler;
        try
        {
            actionHandler = _actionFactory.GetAction(eventName, body, webhookUrl);
        }
        catch (NotImplementedException)
        {
            // 未実装イベント（スタブ以外の未知のイベント）は 400 を返す
            _logger.LogWarning("Unknown event type: {EventName}", eventName);
            return new BadRequestObjectResult(new { message = $"Bad Request: Unknown event '{eventName}'" });
        }

        // アクションが null の場合（未知のイベント）は 400 を返す
        if (actionHandler is null)
        {
            _logger.LogWarning("ActionFactory returned null for event: {EventName}", eventName);
            return new BadRequestObjectResult(new { message = $"Bad Request: Unknown event '{eventName}'" });
        }

        try
        {
            await actionHandler.RunAsync();
            return new OkResult();
        }
        catch (NotImplementedException)
        {
            // スタブアクションはまだ実装されていないため 406 を返す
            _logger.LogInformation("Method not implemented for event: {EventName}", eventName);
            return new ObjectResult(new { message = "Method not implemented" }) { StatusCode = 406 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing event: {EventName}", eventName);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// GitHub イベント名として有効な文字（英小文字・アンダースコア・ハイフン・英数字）のみ許可。
    /// ログインジェクション攻撃を防ぐ。
    /// </summary>
    private static string SanitizeEventName(string raw)
        => System.Text.RegularExpressions.Regex.Replace(raw, "[^a-zA-Z0-9_-]", string.Empty);

    /// <summary>
    /// GitHub イベント名を小文字に正規化する。
    /// GitHub のイベント名は仕様上小文字のため ToLowerInvariant を使用する。
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "GitHub イベント名は仕様上小文字のため ToLowerInvariant が正しい")]
    private static string NormalizeEventName(string eventName)
        => eventName.ToLowerInvariant();

    /// <summary>SSRF 対策: discord.com Webhook URL プレフィックスのみ許可。</summary>
    private static bool IsAllowedWebhookUrl(string url)
        => url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
}
