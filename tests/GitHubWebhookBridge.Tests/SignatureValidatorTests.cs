using System.Security.Cryptography;
using System.Text;
using GitHubWebhookBridge.Utils;
using Microsoft.AspNetCore.Http;
using Moq;

namespace GitHubWebhookBridge.Tests;

public class SignatureValidatorTests
{
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    private static IHeaderDictionary MakeHeaders(string sig)
    {
        var mock = new Mock<IHeaderDictionary>();
        mock.Setup(h => h["X-Hub-Signature-256"])
            .Returns(new Microsoft.Extensions.Primitives.StringValues(sig));
        return mock.Object;
    }

    [Fact]
    public void Validate_ValidSignature_ReturnsTrue()
    {
        var body   = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        var sig    = ComputeSignature(body, secret);
        Assert.True(SignatureValidator.Validate(body, MakeHeaders(sig), secret));
    }

    [Fact]
    public void Validate_InvalidSignature_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        Assert.False(SignatureValidator.Validate(body, MakeHeaders("sha256=000000"), "mysecret"));
    }

    [Fact]
    public void Validate_MissingHeader_ReturnsFalse()
    {
        var mock = new Mock<IHeaderDictionary>();
        mock.Setup(h => h["X-Hub-Signature-256"])
            .Returns(Microsoft.Extensions.Primitives.StringValues.Empty);
        Assert.False(SignatureValidator.Validate([], mock.Object, "secret"));
    }

    [Fact]
    public void Validate_UppercaseHexSignature_ReturnsTrue()
    {
        var body   = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        // 大文字 HEX で署名を生成してもクライアント実装差を吸収して検証に成功する
        using var hmac    = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed      = Convert.ToHexString(hmac.ComputeHash(body)); // UpperInvariant
        var sigUppercase  = $"sha256={computed}";
        Assert.True(SignatureValidator.Validate(body, MakeHeaders(sigUppercase), secret));
    }
}
