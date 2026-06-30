using System.Diagnostics.CodeAnalysis;
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
    /// <see cref="HttpRequestData"/> / <see cref="HttpResponseData"/> ベースの標準 HTTP トリガーを使用する
    /// </remarks>
    /// <param name="req">Azure Functions が受け取った HTTP リクエスト</param>
    /// <returns>処理結果を表す <see cref="HttpResponseData"/></returns>
    [Function("GitHubWebhook")]
    [SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Webhook エントリーポイントは必然的に多くの型を参照する")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "/")] HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // リクエストボディの上限サイズ (10 MB)
        const long MaxBodyBytes = 10L * 1024 * 1024;

        // 1. Content-Length 事前チェック（10MB 超過は Bad Request）
        if (TryGetContentLength(req, out var declaredLength) && declaredLength > MaxBodyBytes)
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Body too large").ConfigureAwait(false);

        // 2. ボディを MaxBodyBytes まで逐次読み取り
        using var ms = new MemoryStream();
        var chunk = new byte[81920];
        int bytesRead;
        while ((bytesRead = await req.Body.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            await ms.WriteAsync(chunk.AsMemory(0, bytesRead)).ConfigureAwait(false);
            if (ms.Length > MaxBodyBytes)
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Body too large").ConfigureAwait(false);
        }

        var rawBody = ms.ToArray();

        if (rawBody.Length == 0)
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Empty body").ConfigureAwait(false);

        // 3. HMAC-SHA256 署名検証
        var secret = _config["GITHUB_WEBHOOK_SECRET"]
            ?? throw new InvalidOperationException("GITHUB_WEBHOOK_SECRET not set");
        var signatureHeader = GetHeaderValue(req, "X-Hub-Signature-256");
        if (!SignatureValidator.Validate(rawBody, signatureHeader, secret))
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid X-Hub-Signature-256").ConfigureAwait(false);

        // 4. X-GitHub-Event ヘッダー検証（ログインジェクション防止のためサニタイズ）
        var rawEventName = GetHeaderValue(req, "X-GitHub-Event");
        if (string.IsNullOrEmpty(rawEventName))
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Missing X-GitHub-Event").ConfigureAwait(false);

        var sanitizedEventName = SanitizeEventName(rawEventName);
        if (sanitizedEventName != rawEventName)
        {
            _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid X-GitHub-Event").ConfigureAwait(false);
        }

        // ActionFactory のリフレクションレジストリは Ordinal 比較のため小文字に正規化する
        var eventName = NormalizeEventName(rawEventName);

        // 5. ?url= — discord.com Webhook URL に限定（SSRF 対策）
        Uri webhookUrl;
        var urlParam = req.Query["url"];
        if (!string.IsNullOrEmpty(urlParam))
        {
            if (!IsAllowedWebhookUrl(urlParam))
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid url parameter").ConfigureAwait(false);
            webhookUrl = new Uri(urlParam);
        }
        else
        {
            // ?url= が省略された場合は環境変数のデフォルト Webhook URL を使用
            var defaultUrl = _config["DISCORD_WEBHOOK_URL"]
                ?? throw new InvalidOperationException("DISCORD_WEBHOOK_URL not set");
            webhookUrl = new Uri(defaultUrl);
        }

        // 6. ?disabled-events= チェック（カンマ区切り、イベント名が含まれる場合は 202 を返す）
        var disabledEventsParam = req.Query["disabled-events"];
        var disabledEvents = !string.IsNullOrEmpty(disabledEventsParam)
            ? disabledEventsParam
            : _config["DISABLED_EVENTS"];
        if (!string.IsNullOrEmpty(disabledEvents))
        {
            var disabledEventNames = disabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (disabledEventNames.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                return await CreateJsonResponseAsync(req, HttpStatusCode.Accepted, "Disabled event").ConfigureAwait(false);
        }

        // 7. JSON 妥当性を維持しつつ raw string として保持する（400 レスポンスを維持するため）
        string rawJson;
        try
        {
            rawJson = Encoding.UTF8.GetString(rawBody);
            // 軽量バリデーション: JSON オブジェクトとして開始しているか確認する
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: JSON body must be an object").ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid JSON body").ConfigureAwait(false);
        }

        // 8. ミュートチェック（Id が null の場合はスキップ — 非数値 id への安全なフォールバック）
        await _muteManager.EnsureLoadedAsync().ConfigureAwait(false);
        WebhookEnvelope? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawJson, OctokitJsonOptions.Value);
        }
        catch (JsonException)
        {
            // デシリアライズ失敗時はミュートチェックをスキップして処理を続行する
        }
        if (envelope?.Sender?.Id is { } senderId)
        {
            if (_muteManager.IsMuted(senderId, eventName, envelope.Action))
                return await CreateJsonResponseAsync(req, HttpStatusCode.OK, "Muted user").ConfigureAwait(false);
        }

        // 9-10. アクションハンドラーへディスパッチ
        IAction actionHandler;
        try
        {
            actionHandler = _actionFactory.GetAction(eventName, rawJson, webhookUrl);
        }
        catch (NotImplementedException)
        {
            // 未実装イベント（スタブ以外の未知のイベント）は 400 を返す
            _logger.LogWarning("Unknown event type: {EventName}", eventName);
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, $"Bad Request: Unknown event '{eventName}'").ConfigureAwait(false);
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
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotAcceptable, "Method not implemented").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing event: {EventName}", eventName);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Content-Length ヘッダーの値を取得する。未設定または非数値の場合は <see langword="false"/> を返す</summary>
    private static bool TryGetContentLength(HttpRequestData req, out long length)
    {
        if (req.Headers.TryGetValues("Content-Length", out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), out var parsed))
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
    /// <c>{ "message": ... }</c> 形式の JSON レスポンスを生成する。
    /// <see cref="HttpResponseData.WriteAsJsonAsync{T}(T, CancellationToken)"/> は
    /// <c>WorkerOptions.Serializer</c> の DI 解決に依存するため、
    /// それを必要としない <c>WriteStringAsync</c> ベースで明示的にシリアライズする
    /// </summary>
    private static async Task<HttpResponseData> CreateJsonResponseAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        HttpResponseData response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        var json = JsonSerializer.Serialize(new { message });
        await response.WriteStringAsync(json).ConfigureAwait(false);
        return response;
    }

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
