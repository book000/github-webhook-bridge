using System.Security.Cryptography;
using System.Text;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>SignatureValidator.Validate() の署名検証テスト。</summary>
public class SignatureValidatorTests
{
    /// <summary>HMAC-SHA256 署名を計算する。</summary>
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    /// <summary>正しい署名は true を返す。</summary>
    [Fact]
    public void ValidateValidSignatureReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        var sig = ComputeSignature(body, secret);
        Assert.True(SignatureValidator.Validate(body, sig, secret));
    }

    /// <summary>不正な署名は false を返す。</summary>
    [Fact]
    public void ValidateInvalidSignatureReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        Assert.False(SignatureValidator.Validate(body, "sha256=000000", "mysecret"));
    }

    /// <summary>署名ヘッダーが未設定（null）の場合は false を返す。</summary>
    [Fact]
    public void ValidateMissingHeaderReturnsFalse()
    {
        Assert.False(SignatureValidator.Validate([], null, "secret"));
    }

    /// <summary>大文字 HEX の署名でもクライアント実装差を吸収して検証に成功する。</summary>
    [Fact]
    public void ValidateUppercaseHexSignatureReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(body)); // UpperInvariant
        var sigUppercase = $"sha256={computed}";
        Assert.True(SignatureValidator.Validate(body, sigUppercase, secret));
    }
}
