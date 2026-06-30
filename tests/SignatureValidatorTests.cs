using System.Security.Cryptography;
using System.Text;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

public class SignatureValidatorTests
{
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    [Fact]
    public void ValidateValidSignatureReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        var sig = ComputeSignature(body, secret);
        Assert.True(SignatureValidator.Validate(body, sig, secret));
    }

    [Fact]
    public void ValidateInvalidSignatureReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        Assert.False(SignatureValidator.Validate(body, "sha256=000000", "mysecret"));
    }

    [Fact]
    public void ValidateMissingHeaderReturnsFalse()
    {
        Assert.False(SignatureValidator.Validate([], null, "secret"));
    }

    [Fact]
    public void ValidateUppercaseHexSignatureReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        // 大文字 HEX で署名を生成してもクライアント実装差を吸収して検証に成功する
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(body)); // UpperInvariant
        var sigUppercase = $"sha256={computed}";
        Assert.True(SignatureValidator.Validate(body, sigUppercase, secret));
    }
}
