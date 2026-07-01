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

/// <summary>Azure Functions class that receives GitHub webhooks and notifies Discord.</summary>
/// <remarks>Receives dependent services via constructor injection.</remarks>
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
    /// Receives a GitHub webhook request and notifies Discord after signature validation and mute checks.
    /// </summary>
    /// <remarks>
    /// The ASP.NET Core integration in <c>Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore</c>
    /// is not used because it has a known, unresolved bug on the Windows Consumption plan
    /// ("Timed out waiting for the function start call", Azure/azure-functions-dotnet-worker#3348).
    /// The standard HTTP trigger based on <see cref="HttpRequestData"/> / <see cref="HttpResponseData"/> is used instead.
    /// <c>Route = ""</c> (empty string) is not used because, even combined with <c>routePrefix = ""</c>, it falls back
    /// to the function name (<c>/githubwebhook</c>) — a known behavior. Matching an empty segment with a regular
    /// expression binds to the literal root path (<c>/</c>) instead.
    /// </remarks>
    /// <param name="req">The HTTP request received by Azure Functions.</param>
    /// <returns>An <see cref="HttpResponseData"/> representing the result of processing.</returns>
    [Function("GitHubWebhook")]
    [SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "The webhook entry point inherently references many types")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{x:regex(^$)?}")] HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // Maximum request body size (10 MB)
        const long MaxBodyBytes = 10L * 1024 * 1024;

        // The Content-Length header can be spoofed, so in addition to pre-checking the declared value, re-check the limit against the actual bytes read.
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

        // To prevent log injection, reject any X-GitHub-Event header that contains characters outside the allowed set.
        var rawEventName = GetHeaderValue(req, "X-GitHub-Event");
        if (string.IsNullOrEmpty(rawEventName))
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Missing X-GitHub-Event").ConfigureAwait(false);

        var sanitizedEventName = SanitizeEventName(rawEventName);
        if (sanitizedEventName != rawEventName)
        {
            _logger.LogWarning("Rejected request with invalid X-GitHub-Event header value");
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.BadRequest, "Bad Request: Invalid X-GitHub-Event").ConfigureAwait(false);
        }

        // ActionFactory's reflection registry uses Ordinal comparison, so normalize to lowercase.
        var eventName = NormalizeEventName(rawEventName);

        // As an SSRF countermeasure, allow only discord.com / discordapp.com webhook URLs.
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

        // When ?disabled-events= is omitted, fall back to the DISABLED_EVENTS environment variable.
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

        // Verify the body is valid JSON before deserialization, while also keeping the raw string for later processing.
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
            // If deserialization fails (e.g., sender.id is non-numeric), skip the mute check and continue processing.
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
            // Return 400 for unimplemented events (unknown events other than stubs).
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
            // Stub actions are not yet implemented, so return 406.
            _logger.LogInformation("Method not implemented for event: {EventName}", eventName);
            return await JsonResponseHelper.CreateAsync(req, HttpStatusCode.NotAcceptable, "Method not implemented").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing event: {EventName}", eventName);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Gets the value of the Content-Length header. Returns <see langword="false"/> if it is unset, non-numeric, or negative.</summary>
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

    /// <summary>Gets the value of the specified header. Returns <see langword="null"/> if it is unset.</summary>
    private static string? GetHeaderValue(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// Allows only characters that are valid in a GitHub event name (letters, underscores, hyphens, and digits).
    /// Prevents log injection attacks.
    /// </summary>
    private static string SanitizeEventName(string raw)
        => Regex.Replace(raw, "[^a-zA-Z0-9_-]", string.Empty);

    /// <summary>
    /// Normalizes a GitHub event name to lowercase.
    /// GitHub event names are lowercase by specification, so ToLowerInvariant is used.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "GitHub event names are lowercase by specification, so ToLowerInvariant is correct")]
    private static string NormalizeEventName(string eventName)
        => eventName.ToLowerInvariant();

    /// <summary>SSRF countermeasure: allow only discord.com webhook URL prefixes.</summary>
    private static bool IsAllowedWebhookUrl(string url)
        => url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
}
