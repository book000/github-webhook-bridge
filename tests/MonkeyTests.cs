using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit.Webhooks.Events;

namespace GitHubWebhookBridge.Tests;

/// <summary>Boundary-value and monkey tests. Confirm that unexpected input does not cause a crash.</summary>
public class MonkeyTests
{
    // ---- SignatureValidator monkey tests ----

    /// <summary>An empty body does not throw.</summary>
    [Fact]
    public void SignatureValidatorEmptyBodyDoesNotThrow()
    {
        var secret = "secret";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        var sig = "sha256=" + Convert.ToHexString(hmac.ComputeHash([])).ToLowerInvariant();

        Exception? ex = Record.Exception(() => SignatureValidator.Validate([], sig, secret));

        Assert.Null(ex);
    }

    /// <summary>A very long signature header (1000 characters) returns false without throwing.</summary>
    [Fact]
    public void SignatureValidatorVeryLongSignatureHeaderReturnsFalseNoThrow()
    {
        var body = Encoding.UTF8.GetBytes("test");
        var longSig = "sha256=" + new string('a', 993); // 1000 characters total

        var result = SignatureValidator.Validate(body, longSig, "secret");

        Assert.False(result);
    }

    /// <summary>An all-zero byte array returns a deterministic result (no exception).</summary>
    [Fact]
    public void SignatureValidatorAllZeroBodyReturnsDeterministicResult()
    {
        var body = new byte[256];
        var secret = "secret";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        var validSig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

        var result1 = SignatureValidator.Validate(body, validSig, secret);
        var result2 = SignatureValidator.Validate(body, validSig, secret);

        Assert.True(result1);
        Assert.Equal(result1, result2);
    }

    /// <summary>A header value with non-ASCII strings (e.g. incomplete %XX encoding) does not throw.</summary>
    [Fact]
    public void SignatureValidatorWeirdHeaderValueReturnsFalseNoThrow()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // Incomplete signature (prefix only)
        var result = SignatureValidator.Validate(body, "sha256=", "secret");

        Assert.False(result);
    }

    // ---- MessageCacheService.SanitizeRowKey monkey tests ----

    private static string InvokeSanitizeRowKey(string key)
    {
        MethodInfo method = typeof(MessageCacheService).GetMethod(
            "SanitizeRowKey",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [key])!;
    }

    /// <summary>Passing an empty string returns an empty string without throwing.</summary>
    [Fact]
    public void SanitizeRowKeyEmptyStringReturnsEmpty()
    {
        var result = InvokeSanitizeRowKey("");

        Assert.Equal("", result);
    }

    /// <summary>A string exceeding 512 characters is truncated to 512 characters or fewer.</summary>
    [Fact]
    public void SanitizeRowKeyLongStringTruncatesToMax512()
    {
        string longKey = new('a', 1000);

        var result = InvokeSanitizeRowKey(longKey);

        Assert.Equal(512, result.Length);
    }

    /// <summary>Azure Table Storage forbidden characters (/ \ # ?) are URL-encoded.</summary>
    [Fact]
    public void SanitizeRowKeyForbiddenCharsUrlEncoded()
    {
        var key = "path/with\\hash#and?query";

        var result = InvokeSanitizeRowKey(key);

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("#", result);
        Assert.DoesNotContain("?", result);
    }

    /// <summary>A %XX encoded triplet is not split at the truncation boundary.</summary>
    [Fact]
    public void SanitizeRowKeyTruncationDoesNotSplitPercentEncoding()
    {
        // Uri.EscapeDataString encodes non-ASCII characters in %XX%XX form.
        // Confirm that the truncated tail does not end with % or %X.
        string key = new('あ', 300); // Each character encodes to %E3%81%82 (9 bytes)

        var result = InvokeSanitizeRowKey(key);

        // Not cut off in the middle of a %XX (the tail is not % or %[0-9A-F])
        Assert.Matches(@"^([^%]|%[0-9A-Fa-f]{2})*$", result);
    }

    /// <summary>Japanese strings are correctly URL-encoded.</summary>
    [Fact]
    public void SanitizeRowKeyJapaneseCharsUrlEncoded()
    {
        var key = "テスト";

        var result = InvokeSanitizeRowKey(key);

        // Because it is encoded, the original Japanese characters are not present
        Assert.DoesNotContain("テスト", result);
        // Contains %XX-form encoded strings
        Assert.Contains("%", result);
    }

    // ---- MuteManager boundary-value tests ----

    private static MuteManager CreateMuteManager(string json)
    {
        Mock<IConfiguration> config = new();
        config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
        config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
        config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
        Mock<IHttpClientFactory> factory = new();
        MuteManager mgr = new(config.Object, factory.Object);
        mgr.LoadForTest(json);
        return mgr;
    }

    /// <summary>With an empty mute list, every user returns false.</summary>
    [Fact]
    public void MuteManagerEmptyListReturnsFalse()
    {
        MuteManager mgr = CreateMuteManager("[]");

        Assert.False(mgr.IsMuted(1, "push", null));
        Assert.False(mgr.IsMuted(long.MaxValue, "issues", "opened"));
    }

    /// <summary>With type = "all", it returns true even when the event/action list is empty.</summary>
    [Fact]
    public void MuteManagerTypeAllEmptyEventsListReturnsTrue()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":42,"type":"all","events":[]}]""");

        Assert.True(mgr.IsMuted(42, "push", null));
        Assert.True(mgr.IsMuted(42, "issues", "opened"));
    }

    /// <summary>userId = 0 does not crash.</summary>
    [Fact]
    public void MuteManagerUserIdZeroDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":0,"type":"all","events":[]}]""");

        Exception? ex = Record.Exception(() => mgr.IsMuted(0, "push", null));

        Assert.Null(ex);
        Assert.True(mgr.IsMuted(0, "push", null));
    }

    /// <summary>userId = long.MaxValue does not crash.</summary>
    [Fact]
    public void MuteManagerUserIdMaxValueDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("[]");

        Exception? ex = Record.Exception(() => mgr.IsMuted(long.MaxValue, "push", null));

        Assert.Null(ex);
    }

    /// <summary>Passing an empty string for eventName does not crash.</summary>
    [Fact]
    public void MuteManagerEmptyEventNameDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":1,"type":"include","events":[{"eventName":"push","actions":null}]}]""");

        Exception? ex = Record.Exception(() => mgr.IsMuted(1, "", null));

        Assert.Null(ex);
    }

    // ---- UnhandledAction boundary-value tests ----

    /// <summary>UnhandledAction.RunAsync throws NotImplementedException (guarantees the HTTP 406 path).</summary>
    [Fact]
    public async Task UnhandledActionRunAsyncThrowsNotImplementedException()
    {
        var action = new UnhandledAction("check_run");

        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    // ---- PullRequestAction boundary-value tests ----

    private static readonly Uri _actionWebhookUri = new("https://discord.com/api/webhooks/1/x");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateActionMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static PullRequestEvent MakePrEvent(string action = "opened", string? body = null)
    {
        var prJson = TestFixtures.PullRequestJson(number: 1, title: "Test PR", body: body);
        var repoJson = TestFixtures.RepoJson("owner/repo");
        var senderJson = TestFixtures.UserJson("author", 100);
        // Always include before/after because PullRequestSynchronizeEvent requires them when action="synchronize"
        return JsonSerializer.Deserialize<PullRequestEvent>(
            $$$"""{"action":"{{{action}}}","number":1,"before":"aaa000","after":"bbb111","pull_request":{{{prJson}}},"repository":{{{repoJson}}},"sender":{{{senderJson}}}}""",
            OctokitJsonOptions.Value)!;
    }

    /// <summary>A null PR Body and null RequestedReviewers do not cause a NullReferenceException.</summary>
    [Fact]
    public async Task PullRequestActionNullBodyAndNullRequestedReviewersDoesNotThrow()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateActionMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _actionWebhookUri, "pull_request", MakePrEvent("opened", body: null));

        Exception? ex = await Record.ExceptionAsync(() => action.RunAsync());

        Assert.Null(ex);
    }

    /// <summary>The synchronize action completes successfully without sending to Discord.</summary>
    [Fact]
    public async Task PullRequestActionSynchronizeActionSendsNoMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateActionMocks();

        PullRequestAction action = new(
            discord.Object, cache.Object, userMap.Object,
            Mock.Of<ILogger<PullRequestAction>>(),
            _actionWebhookUri, "pull_request", MakePrEvent("synchronize"));

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
    }
}
