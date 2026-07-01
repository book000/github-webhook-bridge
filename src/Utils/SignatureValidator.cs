using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace GitHubWebhookBridge.Utils;

/// <summary>Utility class that validates the HMAC-SHA256 signature of a GitHub Webhook</summary>
public static class SignatureValidator
{
    private const string SignaturePrefix = "sha256=";

    /// <summary>
    /// Validates the X-Hub-Signature-256 header value against the raw request body.
    /// Uses <see cref="CryptographicOperations.FixedTimeEquals"/> to prevent timing attacks.
    /// Performs a dummy comparison even when the lengths differ, so length information does not leak via timing
    /// </summary>
    /// <param name="rawBody">HTTP request body bytes to validate</param>
    /// <param name="signatureHeader">Value of the X-Hub-Signature-256 header (null if not set)</param>
    /// <param name="secret">Secret string used to compute the HMAC-SHA256 signature</param>
    /// <returns><see langword="true"/> if the signature is valid; <see langword="false"/> if it is invalid or the signature header does not exist</returns>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "GitHub Webhook signatures are lowercase hex per spec, so ToLowerInvariant is correct")]
    public static bool Validate(byte[] rawBody, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Normalize to lowercase to absorb client implementation differences (e.g. uppercase HEX)
        var receivedHashHex = signatureHeader[SignaturePrefix.Length..].ToLowerInvariant();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHashHex = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();

        var computedHashBytes = Encoding.ASCII.GetBytes(computedHashHex);
        var receivedHashBytes = Encoding.ASCII.GetBytes(receivedHashHex);

        // Always run FixedTimeEquals so that timing information does not leak even when the lengths differ.
        // Normalize receivedHashBytes to the same length as computedHashBytes before comparing.
        var normalizedReceived = new byte[computedHashBytes.Length];
        var copyLength = Math.Min(receivedHashBytes.Length, normalizedReceived.Length);
        receivedHashBytes.AsSpan(0, copyLength).CopyTo(normalizedReceived);

        var equal = CryptographicOperations.FixedTimeEquals(computedHashBytes, normalizedReceived);

        // False if the lengths differ (because normalization changed the content)
        return equal && receivedHashBytes.Length == computedHashBytes.Length;
    }
}
