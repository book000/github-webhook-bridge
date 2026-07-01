using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Functions;

/// <summary>GitHub Webhook を受信し Discord に通知する Azure Functions のクラス</summary>
/// <remarks>依存サービスをコンストラクタインジェクションで受け取る</remarks>
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

    /// <summary>
    /// GitHub Webhook リクエストを受け取り、署名検証・ミュートチェックを経て Discord に通知する
    /// </summary>
    /// <remarks>
    /// <c>Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore</c> の ASP.NET Core 統合は
    /// Windows Consumption プランで「Timed out waiting for the function start call」という既知の
    /// 未解決バグ（Azure/azure-functions-dotnet-worker#3348）を抱えているため使用しない。
    /// <see cref="HttpRequestData"/> / <see cref="HttpResponseData"/> ベースの標準 HTTP トリガーを使用する。
    /// <c>Route = ""</c>（空文字）は <c>routePrefix = ""</c> と組み合わせても関数名 (<c>/githubwebhook</c>)
    /// にフォールバックしてしまう既知の挙動のため使用しない。正規表現で空セグメントにマッチさせることで
    /// 文字通りのルートパス（<c>/</c>）にバインドする
    /// </remarks>
    /// <param name="req">Azure Functions が受け取った HTTP リクエスト</param>
    /// <returns>処理結果を表す <see cref="HttpResponseData"/></returns>
    [Function("GitHubWebhook")]
    [SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Webhook エントリーポイントは必然的に多くの型を参照する")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{x:regex(^$)?}")] HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // リクエストボディの上限サイズ (10 MB)
        const long MaxBodyBytes = 10L * 1024 * 1024;

        // Content-Length ヘッダーは偽装され得るため、宣言値の事前チェックに加えて実読み取りバイト数でも上限を再チェックする
        if (TryGetContentLength(req, out var declaredLength) && declaredLength > MaxBodyBytes)
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Body too large").ConfigureAwait(false);

        using var ms = new MemoryStream();
        var chunk = new byte[81920];
        int bytesRead;
        while ((bytesRead = await req.Body.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            await ms.WriteAsync(chunk.AsMemory(0, bytesRead)).ConfigureAwait(false);
            if (ms.Length > MaxBodyBytes)
                return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Body too large").ConfigureAwait(false);
        }

        var rawBody = ms.ToArray();

        if (rawBody.Length == 0)
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Empty body").ConfigureAwait(false);

        var secret = _config["GITHUB_WEBHOOK_SECRET"]
            ?? throw new InvalidOperationException("GITHUB_WEBHOOK_SECRET not set");
        var signatureHeader = GetHeaderValue(req, "X-Hub-Signature-256");
        if (!SignatureValidator.Validate(rawBody, signatureHeader, secret))
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid X-Hub-Signature-256").ConfigureAwait(false);

        // ログインジェクション防止のため、許可文字以外を含む X-GitHub-Event ヘッダーは拒否する
        var rawEventName = GetHeaderValue(req, "X-GitHub-Event");
        if (string.IsNullOrEmpty(rawEventName))
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Missing X-GitHub-Event").ConfigureAwait(false);

        var sanitizedEventName = SanitizeEventName(rawEventName);
        if (sanitizedEventName != rawEventName)
        {
            _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid X-GitHub-Event").ConfigureAwait(false);
        }

        // ActionFactory のリフレクションレジストリは Ordinal 比較のため小文字に正規化する
        var eventName = NormalizeEventName(rawEventName);

        // SSRF 対策として discord.com / discordapp.com の Webhook URL のみ許可する
        Uri webhookUrl;
        var urlParam = req.Query["url"];
        if (!string.IsNullOrEmpty(urlParam))
        {
            if (!IsAllowedWebhookUrl(urlParam))
                return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid url parameter").ConfigureAwait(false);
            webhookUrl = new Uri(urlParam);
        }
        else
        {
            var defaultUrl = _config["DISCORD_WEBHOOK_URL"]
                ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set");
            webhookUrl = new Uri(defaultUrl);
        }

        // ?disabled-events= が省略された場合は環境変数 DISABLED_EVENTS にフォールバックする
        var disabledEventsParam = req.Query["disabled-events"];
        var disabledEvents = !string.IsNullOrEmpty(disabledEventsParam)
            ? disabledEventsParam
            : _config["DISABLED_EVENTS"];
        if (!string.IsNullOrEmpty(disabledEvents))
        {
            var disabledEventNames = disabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (disabledEventNames.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.Accepted, "Disabled event").ConfigureAwait(false);
        }

        // デシリアライズ前に JSON として妥当か確認しつつ、後続処理のために raw string も保持する
        string rawJson;
        try
        {
            rawJson = Encoding.UTF8.GetString(rawBody);
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: JSON body must be an object").ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid JSON body").ConfigureAwait(false);
        }

        await _muteManager.EnsureLoadedAsync().ConfigureAwait(false);
        WebhookEnvelope? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawJson, OctokitJsonOptions.Value);
        }
        catch (JsonException)
        {
            // デシリアライズに失敗した場合（例: sender.id が非数値）はミュートチェックをスキップして処理を続行する
        }

        if (envelope?.Sender?.Id is { } senderId)
        {
            if (_muteManager.IsMuted(senderId, eventName, envelope.Action))
                return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.OK, "Muted user").ConfigureAwait(false);
        }

        IAction actionHandler;
        try
        {
            actionHandler = _actionFactory.GetAction(eventName, rawJson, webhookUrl);
        }
        catch (NotImplementedException)
        {
            // 未実装イベント（スタブ以外の未知のイベント）は 400 を返す
            _logger.LogWarning("Unknown event type: {EventName}", eventName);
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, $"Bad Request: Unknown event '{eventName}'").ConfigureAwait(false);
        }

        try
        {
            await actionHandler.RunAsync().ConfigureAwait(false);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (NotImplementedException)
        {
            // スタブアクションはまだ実装されていないため 406 を返す
            _logger.LogInformation("Method not implemented for event: {EventName}", eventName);
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.NotAcceptable, "Method not implemented").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing event: {EventName}", eventName);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Content-Length ヘッダーの値を取得する。未設定・非数値・負数の場合は <see langword="false"/> を返す</summary>
    private static bool TryGetContentLength(HttpRequestData req, out long length)
    {
        if (req.Headers.TryGetValues("Content-Length", out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0)
        {
            length = parsed;
            return true;
        }

        length = 0;
        return false;
    }

    /// <summary>指定ヘッダーの値を取得する。未設定の場合は <see langword="null"/> を返す</summary>
    private static string? GetHeaderValue(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// GitHub イベント名として有効な文字（英小文字・アンダースコア・ハイフン・英数字）のみ許可する。
    /// ログインジェクション攻撃を防ぐ
    /// </summary>
    private static string SanitizeEventName(string raw)
        => Regex.Replace(raw, "[^a-zA-Z0-9_-]", string.Empty);

    /// <summary>
    /// GitHub イベント名を小文字に正規化する。
    /// GitHub のイベント名は仕様上小文字のため ToLowerInvariant を使用する
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "GitHub イベント名は仕様上小文字のため ToLowerInvariant が正しい")]
    private static string NormalizeEventName(string eventName)
        => eventName.ToLowerInvariant();

    /// <summary>SSRF 対策: discord.com Webhook URL プレフィックスのみ許可する</summary>
    private static bool IsAllowedWebhookUrl(string url)
        => url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
}
