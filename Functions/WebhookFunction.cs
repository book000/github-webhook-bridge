using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Functions;

/// <summary>GitHub Webhook を受信し Discord に通知する Azure Function。</summary>
public class WebhookFunction
{
    private readonly IActionFactory          _actionFactory;
    private readonly IMuteManager            _muteManager;
    private readonly IConfiguration          _config;
    private readonly ILogger<WebhookFunction> _logger;

    public WebhookFunction(
        IActionFactory          actionFactory,
        IMuteManager            muteManager,
        IConfiguration          config,
        ILogger<WebhookFunction> logger)
    {
        _actionFactory = actionFactory;
        _muteManager   = muteManager;
        _config        = config;
        _logger        = logger;
    }

    [Function("GitHubWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "GitHubWebhook")] HttpRequest req)
    {
        // リクエストボディの上限サイズ (10 MB)
        const long MaxBodyBytes = 10L * 1024 * 1024;

        // 1. Content-Length 事前チェック（10MB 超過は Bad Request）
        if (req.ContentLength.HasValue && req.ContentLength.Value > MaxBodyBytes)
            return new BadRequestObjectResult(new { message = "Bad Request: Body too large" });

        // 2. ボディを MaxBodyBytes まで逐次読み取り
        req.EnableBuffering();
        using var ms    = new MemoryStream();
        var       chunk = new byte[81920];
        int bytesRead;
        while ((bytesRead = await req.Body.ReadAsync(chunk)) > 0)
        {
            ms.Write(chunk, 0, bytesRead);
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
            return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-Hub-Signature" });

        // 4. X-GitHub-Event ヘッダー検証（ログインジェクション防止のためサニタイズ）
        var rawEventName = req.Headers["X-GitHub-Event"].ToString();
        if (string.IsNullOrEmpty(rawEventName))
            return new BadRequestObjectResult(new { message = "Bad Request: Missing X-GitHub-Event" });

        var eventName = SanitizeEventName(rawEventName);
        if (eventName != rawEventName)
        {
            _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
            return new BadRequestObjectResult(new { message = "Bad Request: Invalid X-GitHub-Event" });
        }

        // 5. ?url= — discord.com Webhook URL に限定（SSRF 対策）
        string webhookUrl;
        if (req.Query.TryGetValue("url", out var urlParam) && !string.IsNullOrEmpty(urlParam))
        {
            var candidate = urlParam.ToString();
            if (!IsAllowedWebhookUrl(candidate))
                return new BadRequestObjectResult(new { message = "Bad Request: Invalid url parameter" });
            webhookUrl = candidate;
        }
        else
        {
            // ?url= が省略された場合は環境変数のデフォルト Webhook URL を使用
            webhookUrl = _config["DISCORD_WEBHOOK_URL"]
                ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set");
        }

        // 6. ?disabled-events= チェック（カンマ区切り、イベント名が含まれる場合は 202 を返す）
        var disabledEvents = req.Query.TryGetValue("disabled-events", out var deParam)
                             && !string.IsNullOrEmpty(deParam)
            ? deParam.ToString()
            : _config["DISABLED_EVENTS"];
        if (!string.IsNullOrEmpty(disabledEvents))
        {
            var disabled = disabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (disabled.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                return new ObjectResult(new { message = "Disabled event" }) { StatusCode = 202 };
        }

        // 7. JSON デシリアライズ
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        var body    = JsonSerializer.Deserialize<JsonElement>(rawBody, options);

        // 8. 送信者ミュートチェック
        await _muteManager.EnsureLoadedAsync();
        if (body.TryGetProperty("sender", out var sender)
            && sender.TryGetProperty("id", out var senderId))
        {
            var actionProp = body.TryGetProperty("action", out var a) ? a.GetString() : null;
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
        => System.Text.RegularExpressions.Regex.Replace(raw, "[^a-zA-Z0-9_-]", "");

    /// <summary>SSRF 対策: discord.com Webhook URL プレフィックスのみ許可。</summary>
    private static bool IsAllowedWebhookUrl(string url)
        => url.StartsWith("https://discord.com/api/webhooks/",    StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
}
