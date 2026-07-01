using System.Security.Cryptography;
using System.Text;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>Signature validation tests for SignatureValidator.Validate().</summary>
public class SignatureValidatorTests
{
    /// <summary>Computes the HMAC-SHA256 signature.</summary>
    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    /// <summary>A valid signature returns true.</summary>
    [Fact]
    public void ValidateValidSignatureReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var secret = "mysecret";
        var sig = ComputeSignature(body, secret);
        Assert.True(SignatureValidator.Validate(body, sig, secret));
    }

    /// <summary>An invalid signature returns false.</summary>
    [Fact]
    public void ValidateInvalidSignatureReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        Assert.False(SignatureValidator.Validate(body, "sha256=000000", "mysecret"));
    }

    /// <summary>Returns false when the signature header is not set (null).</summary>
    [Fact]
    public void ValidateMissingHeaderReturnsFalse()
    {
        Assert.False(SignatureValidator.Validate([], null, "secret"));
    }

    /// <summary>Validation succeeds even with an uppercase HEX signature, absorbing client implementation differences.</summary>
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
