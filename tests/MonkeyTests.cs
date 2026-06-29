using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Actions.Stubs;
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
        Mock<IHeaderDictionary> mock = new();
        mock.Setup(h => h["X-Hub-Signature-256"])
            .Returns(new Microsoft.Extensions.Primitives.StringValues(sig));
        return mock.Object;
    }

    /// <summary>空のボディでも例外が発生しない。</summary>
    [Fact]
    public void SignatureValidatorEmptyBodyDoesNotThrow()
    {
        var secret = "secret";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        var sig = "sha256=" + Convert.ToHexString(hmac.ComputeHash([])).ToLowerInvariant();

        Exception? ex = Record.Exception(() => SignatureValidator.Validate([], MakeHeaders(sig), secret));

        Assert.Null(ex);
    }

    /// <summary>非常に長い署名ヘッダー（1000 文字）は false を返し、例外が発生しない。</summary>
    [Fact]
    public void SignatureValidatorVeryLongSignatureHeaderReturnsFalseNoThrow()
    {
        var body = Encoding.UTF8.GetBytes("test");
        var longSig = "sha256=" + new string('a', 993); // 合計 1000 文字

        var result = SignatureValidator.Validate(body, MakeHeaders(longSig), "secret");

        Assert.False(result);
    }

    /// <summary>すべてゼロのバイト列でも決定的な結果を返す（非例外）。</summary>
    [Fact]
    public void SignatureValidatorAllZeroBodyReturnsDeterministicResult()
    {
        var body = new byte[256];
        var secret = "secret";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        var validSig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

        var result1 = SignatureValidator.Validate(body, MakeHeaders(validSig), secret);
        var result2 = SignatureValidator.Validate(body, MakeHeaders(validSig), secret);

        Assert.True(result1);
        Assert.Equal(result1, result2);
    }

    /// <summary>ヘッダー値が ASCII 非対応文字列（%XX 不完全エンコード等）でも例外が発生しない。</summary>
    [Fact]
    public void SignatureValidatorWeirdHeaderValueReturnsFalseNoThrow()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // 不完全なシグネチャ（プレフィックスのみ）
        var result = SignatureValidator.Validate(body, MakeHeaders("sha256="), "secret");

        Assert.False(result);
    }

    // ---- MessageCacheService.SanitizeRowKey モンキーテスト ----

    private static string InvokeSanitizeRowKey(string key)
    {
        MethodInfo method = typeof(MessageCacheService).GetMethod(
            "SanitizeRowKey",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [key])!;
    }

    /// <summary>空文字列を渡しても例外が発生せず空文字列を返す。</summary>
    [Fact]
    public void SanitizeRowKeyEmptyStringReturnsEmpty()
    {
        var result = InvokeSanitizeRowKey("");

        Assert.Equal("", result);
    }

    /// <summary>512 文字超の文字列は 512 文字以下に切り詰められる。</summary>
    [Fact]
    public void SanitizeRowKeyLongStringTruncatesToMax512()
    {
        string longKey = new('a', 1000);

        var result = InvokeSanitizeRowKey(longKey);

        Assert.Equal(512, result.Length);
    }

    /// <summary>Azure Table Storage 禁止文字（/ \ # ?）は URL エンコードされる。</summary>
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

    /// <summary>切り詰め境界で %XX エンコード三文字組が分断されない。</summary>
    [Fact]
    public void SanitizeRowKeyTruncationDoesNotSplitPercentEncoding()
    {
        // Uri.EscapeDataString は非 ASCII 文字を %XX%XX 形式でエンコードする。
        // 切り詰め後の末尾が % や %X で終わらないことを確認する。
        string key = new('あ', 300); // 各文字が %E3%81%82 (9 bytes) にエンコードされる

        var result = InvokeSanitizeRowKey(key);

        // %XX の途中で切れていないこと（末尾が % または %[0-9A-F] でないこと）
        Assert.Matches(@"^([^%]|%[0-9A-Fa-f]{2})*$", result);
    }

    /// <summary>日本語文字列は正しく URL エンコードされる。</summary>
    [Fact]
    public void SanitizeRowKeyJapaneseCharsUrlEncoded()
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
        Mock<IConfiguration> config = new();
        config.Setup(c => c["MUTES_FILE_PATH"]).Returns((string?)null);
        config.Setup(c => c["MUTES_FILE_URL"]).Returns((string?)null);
        config.Setup(c => c["MUTES_BLOB"]).Returns((string?)null);
        Mock<IHttpClientFactory> factory = new();
        MuteManager mgr = new(config.Object, factory.Object);
        mgr.LoadForTest(json);
        return mgr;
    }

    /// <summary>空のミュートリストではどのユーザーも false を返す。</summary>
    [Fact]
    public void MuteManagerEmptyListReturnsFalse()
    {
        MuteManager mgr = CreateMuteManager("[]");

        Assert.False(mgr.IsMuted(1, "push", null));
        Assert.False(mgr.IsMuted(long.MaxValue, "issues", "opened"));
    }

    /// <summary>type = "all" でイベント・アクションリストが空でも true を返す。</summary>
    [Fact]
    public void MuteManagerTypeAllEmptyEventsListReturnsTrue()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":42,"type":"all","events":[]}]""");

        Assert.True(mgr.IsMuted(42, "push", null));
        Assert.True(mgr.IsMuted(42, "issues", "opened"));
    }

    /// <summary>userId = 0 でもクラッシュしない。</summary>
    [Fact]
    public void MuteManagerUserIdZeroDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":0,"type":"all","events":[]}]""");

        Exception? ex = Record.Exception(() => mgr.IsMuted(0, "push", null));

        Assert.Null(ex);
        Assert.True(mgr.IsMuted(0, "push", null));
    }

    /// <summary>userId = long.MaxValue でもクラッシュしない。</summary>
    [Fact]
    public void MuteManagerUserIdMaxValueDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("[]");

        Exception? ex = Record.Exception(() => mgr.IsMuted(long.MaxValue, "push", null));

        Assert.Null(ex);
    }

    /// <summary>eventName に空文字列を渡してもクラッシュしない。</summary>
    [Fact]
    public void MuteManagerEmptyEventNameDoesNotThrow()
    {
        MuteManager mgr = CreateMuteManager("""[{"userId":1,"type":"include","events":[{"eventName":"push","actions":null}]}]""");

        Exception? ex = Record.Exception(() => mgr.IsMuted(1, "", null));

        Assert.Null(ex);
    }

    // ---- StubAction 境界値テスト ----

    /// <summary>スタブアクションの RunAsync は NotImplementedException をスローする（HTTP 406 経路の担保）。</summary>
    [Fact]
    public async Task StubActionRunAsyncThrowsNotImplementedException()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateActionMocks();

        // 任意の具象スタブを代表として使用
        var action = new CheckRunAction(
            discord.Object,
            new Uri("https://discord.com/api/webhooks/1/x"),
            "check_run",
            JsonDocument.Parse("{}").RootElement,
            cache.Object,
            userMap.Object,
            Mock.Of<ILogger>());

        await Assert.ThrowsAsync<NotImplementedException>(() => action.RunAsync());
    }

    // ---- PullRequestAction 境界値テスト ----

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

    /// <summary>PR の Body と RequestedReviewers が null でも NullReferenceException が発生しない。</summary>
    [Fact]
    public async Task PullRequestActionNullBodyAndNullRequestedReviewersDoesNotThrow()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateActionMocks();

        PullRequestEvent prEvent = new()
        {
            Action = "opened",
            Number = 1,
            PullRequest = new PullRequest
            {
                Number = 1,
                Title = "Test PR",
                Body = null, // null ボディ
                State = "open",
                HtmlUrl = new Uri("https://github.com/owner/repo/pull/1"),
                User = new User { Login = "author", Id = 100 },
                Draft = false,
                Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
                Base = new PullRequestRef { Ref = "main", Sha = "def" },
                RequestedReviewers = null, // null レビュアーリスト
            },
            Repository = new Repository { FullName = "owner/repo", HtmlUrl = new Uri("https://github.com/owner/repo") },
            Sender = new User { Login = "author", Id = 100 },
        };

        PullRequestAction action = new(
            discord.Object, _actionWebhookUri, "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        Exception? ex = await Record.ExceptionAsync(() => action.RunAsync());

        Assert.Null(ex);
    }

    /// <summary>synchronize アクションは Discord に送信せずに正常終了する。</summary>
    [Fact]
    public async Task PullRequestActionSynchronizeActionSendsNoMessage()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateActionMocks();

        PullRequestEvent prEvent = new()
        {
            Action = "synchronize",
            Number = 1,
            PullRequest = new PullRequest
            {
                Number = 1,
                Title = "Test PR",
                State = "open",
                HtmlUrl = new Uri("https://github.com/owner/repo/pull/1"),
                User = new User { Login = "author", Id = 100 },
                Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
                Base = new PullRequestRef { Ref = "main", Sha = "def" },
            },
            Repository = new Repository { FullName = "owner/repo", HtmlUrl = new Uri("https://github.com/owner/repo") },
            Sender = new User { Login = "author", Id = 100 },
        };

        PullRequestAction action = new(
            discord.Object, _actionWebhookUri, "pull_request",
            prEvent, cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()), Times.Never);
    }
}
