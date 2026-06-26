using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>境界値・モンキーテスト。予期しない入力でクラッシュしないことを確認する。</summary>
public class MonkeyTests
{
    // ---- SignatureValidator モンキーテスト ----

    private static IHeaderDictionary MakeHeaders(string sig)
    {
        var mock = new Mock<IHeaderDictionary>();
        mock.Setup(h => h["X-Hub-Signature-256"])
            .Returns(new Microsoft.Extensions.Primitives.StringValues(sig));
        return mock.Object;
    }

    /// <summary>空のボディでも例外が発生しない。</summary>
    [Fact]
    public void SignatureValidator_EmptyBody_DoesNotThrow()
    {
        var secret = "secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Array.Empty<byte>())).ToLowerInvariant();

        var ex = Record.Exception(() => SignatureValidator.Validate(Array.Empty<byte>(), MakeHeaders(sig), secret));

        Assert.Null(ex);
    }

    /// <summary>非常に長い署名ヘッダー（1000 文字）は false を返し、例外が発生しない。</summary>
    [Fact]
    public void SignatureValidator_VeryLongSignatureHeader_ReturnsFalse_NoThrow()
    {
        var body      = Encoding.UTF8.GetBytes("test");
        var longSig   = "sha256=" + new string('a', 993); // 合計 1000 文字

        var result = SignatureValidator.Validate(body, MakeHeaders(longSig), "secret");

        Assert.False(result);
    }

    /// <summary>すべてゼロのバイト列でも決定的な結果を返す（非例外）。</summary>
    [Fact]
    public void SignatureValidator_AllZeroBody_ReturnsDeterministicResult()
    {
        var body   = new byte[256];
        var secret = "secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var validSig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

        var result1 = SignatureValidator.Validate(body, MakeHeaders(validSig), secret);
        var result2 = SignatureValidator.Validate(body, MakeHeaders(validSig), secret);

        Assert.True(result1);
        Assert.Equal(result1, result2);
    }

    /// <summary>ヘッダー値が ASCII 非対応文字列（%XX 不完全エンコード等）でも例外が発生しない。</summary>
    [Fact]
    public void SignatureValidator_WeirdHeaderValue_ReturnsFalse_NoThrow()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // 不完全なシグネチャ（プレフィックスのみ）
        var result = SignatureValidator.Validate(body, MakeHeaders("sha256="), "secret");

        Assert.False(result);
    }

    // ---- MessageCacheService.SanitizeRowKey モンキーテスト ----

    private static string InvokeSanitizeRowKey(string key)
    {
        var method = typeof(MessageCacheService).GetMethod(
            "SanitizeRowKey",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [key])!;
    }

    /// <summary>空文字列を渡しても例外が発生せず空文字列を返す。</summary>
    [Fact]
    public void SanitizeRowKey_EmptyString_ReturnsEmpty()
    {
        var result = InvokeSanitizeRowKey("");

        Assert.Equal("", result);
    }

    /// <summary>512 文字超の文字列は 512 文字以下に切り詰められる。</summary>
    [Fact]
    public void SanitizeRowKey_LongString_TruncatesToMax512()
    {
        var longKey = new string('a', 1000);

        var result = InvokeSanitizeRowKey(longKey);

        Assert.True(result.Length <= 512);
    }

    /// <summary>Azure Table Storage 禁止文字（/ \ # ?）は URL エンコードされる。</summary>
    [Fact]
    public void SanitizeRowKey_ForbiddenChars_UrlEncoded()
    {
        var key = "path/with\\hash#and?query";

        var result = InvokeSanitizeRowKey(key);

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("#", result);
        Assert.DoesNotContain("?", result);
    }

    /// <summary>切り詰め境界で %XX エンコード三文字組が分断されない。</summary>
    [Fact]
    public void SanitizeRowKey_TruncationDoesNotSplitPercentEncoding()
    {
        // Uri.EscapeDataString は非 ASCII 文字を %XX%XX 形式でエンコードする。
        // 切り詰め後の末尾が % や %X で終わらないことを確認する。
        var key = new string('あ', 300); // 各文字が %E3%81%82 (9 bytes) にエンコードされる

        var result = InvokeSanitizeRowKey(key);

        // %XX の途中で切れていないこと（末尾が % または %[0-9A-F] でないこと）
        Assert.Matches(@"^([^%]|%[0-9A-Fa-f]{2})*$", result);
    }

    /// <summary>日本語文字列は正しく URL エンコードされる。</summary>
    [Fact]
    public void SanitizeRowKey_JapaneseChars_UrlEncoded()
    {
        var key = "テスト";

        var result = InvokeSanitizeRowKey(key);

        // エンコードされているため元の日本語文字が含まれない
        Assert.DoesNotContain("テスト", result);
        // %XX 形式のエンコード文字列が含まれる
        Assert.Contains("%", result);
    }

    // ---- MuteManager 境界値テスト ----

    private static MuteManager CreateMuteManager(string json)
    {
        var config  = new Mock<IConfiguration>();
        config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
        config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
        config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
        var factory = new Mock<IHttpClientFactory>();
        var mgr     = new MuteManager(config.Object, factory.Object);
        mgr.LoadForTest(json);
        return mgr;
    }

    /// <summary>空のミュートリストではどのユーザーも false を返す。</summary>
    [Fact]
    public void MuteManager_EmptyList_ReturnsFalse()
    {
        var mgr = CreateMuteManager("[]");

        Assert.False(mgr.IsMuted(1, "push", null));
        Assert.False(mgr.IsMuted(long.MaxValue, "issues", "opened"));
    }

    /// <summary>type = "all" でイベント・アクションリストが空でも true を返す。</summary>
    [Fact]
    public void MuteManager_TypeAll_EmptyEventsList_ReturnsTrue()
    {
        var mgr = CreateMuteManager("""[{"userId":42,"type":"all","events":[]}]""");

        Assert.True(mgr.IsMuted(42, "push", null));
        Assert.True(mgr.IsMuted(42, "issues", "opened"));
    }

    /// <summary>userId = 0 でもクラッシュしない。</summary>
    [Fact]
    public void MuteManager_UserIdZero_DoesNotThrow()
    {
        var mgr = CreateMuteManager("""[{"userId":0,"type":"all","events":[]}]""");

        var ex = Record.Exception(() => mgr.IsMuted(0, "push", null));

        Assert.Null(ex);
        Assert.True(mgr.IsMuted(0, "push", null));
    }

    /// <summary>userId = long.MaxValue でもクラッシュしない。</summary>
    [Fact]
    public void MuteManager_UserIdMaxValue_DoesNotThrow()
    {
        var mgr = CreateMuteManager("[]");

        var ex = Record.Exception(() => mgr.IsMuted(long.MaxValue, "push", null));

        Assert.Null(ex);
    }

    /// <summary>eventName に空文字列を渡してもクラッシュしない。</summary>
    [Fact]
    public void MuteManager_EmptyEventName_DoesNotThrow()
    {
        var mgr = CreateMuteManager("""[{"userId":1,"type":"include","events":[{"eventName":"push","actions":null}]}]""");

        var ex = Record.Exception(() => mgr.IsMuted(1, "", null));

        Assert.Null(ex);
    }

    // ---- PullRequestAction 境界値テスト ----

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateActionMocks()
    {
        var discord = new Mock<IDiscordClient>();
        var cache   = new Mock<IMessageCacheService>();
        var userMap = new Mock<IGitHubUserMapManager>();

        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    /// <summary>PR の Body と RequestedReviewers が null でも NullReferenceException が発生しない。</summary>
    [Fact]
    public async Task PullRequestAction_NullBodyAndNullRequestedReviewers_DoesNotThrow()
    {
        var (discord, cache, userMap) = CreateActionMocks();

        var prEvent = new PullRequestEvent
        {
            Action = "opened",
            Number = 1,
            PullRequest = new PullRequest
            {
                Number  = 1,
                Title   = "Test PR",
                Body    = null, // null ボディ
                State   = "open",
                HtmlUrl = "https://github.com/owner/repo/pull/1",
                User    = new User { Login = "author", Id = 100 },
                Draft   = false,
                Head    = new PullRequestRef { Ref = "feature", Sha = "abc" },
                Base    = new PullRequestRef { Ref = "main",    Sha = "def" },
                RequestedReviewers = null, // null レビュアーリスト
            },
            Repository = new Repository { FullName = "owner/repo", HtmlUrl = "https://github.com/owner/repo" },
            Sender     = new User { Login = "author", Id = 100 },
        };

        var action = new PullRequestAction(
            discord.Object, "https://discord.com/api/webhooks/1/x", "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        var ex = await Record.ExceptionAsync(() => action.RunAsync());

        Assert.Null(ex);
    }

    /// <summary>synchronize アクションは Discord に送信せずに正常終了する。</summary>
    [Fact]
    public async Task PullRequestAction_SynchronizeAction_SendsNoMessage()
    {
        var (discord, cache, userMap) = CreateActionMocks();

        var prEvent = new PullRequestEvent
        {
            Action = "synchronize",
            Number = 1,
            PullRequest = new PullRequest
            {
                Number  = 1,
                Title   = "Test PR",
                State   = "open",
                HtmlUrl = "https://github.com/owner/repo/pull/1",
                User    = new User { Login = "author", Id = 100 },
                Head    = new PullRequestRef { Ref = "feature", Sha = "abc" },
                Base    = new PullRequestRef { Ref = "main",    Sha = "def" },
            },
            Repository = new Repository { FullName = "owner/repo", HtmlUrl = "https://github.com/owner/repo" },
            Sender     = new User { Login = "author", Id = 100 },
        };

        var action = new PullRequestAction(
            discord.Object, "https://discord.com/api/webhooks/1/x", "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<string>(), It.IsAny<DiscordMessage>()), Times.Never);
    }
}
