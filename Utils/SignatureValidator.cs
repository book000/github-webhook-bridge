using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace GitHubWebhookBridge.Utils;

/// <summary>GitHub Webhook の HMAC-SHA256 署名を検証するユーティリティ。</summary>
public static class SignatureValidator
{
    private const string SignaturePrefix = "sha256=";

    /// <summary>
    /// X-Hub-Signature-256 ヘッダーを raw リクエストボディと照合して検証する。
    /// タイミング攻撃を防ぐために <see cref="CryptographicOperations.FixedTimeEquals"/> を使用する。
    /// </summary>
    /// <remarks>
    /// computedBytes.Length は常に 64（HMAC-SHA256 hex の定数長）のため、
    /// 長さ不一致のアーリーリターンは攻撃者にタイミング情報を与えない。
    /// </remarks>
    public static bool Validate(byte[] rawBody, IHeaderDictionary headers, string secret)
    {
        var signatureHeader = headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHash = signatureHeader[SignaturePrefix.Length..];

        using var hmac        = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var       computedHash = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();

        var computedBytes = Encoding.ASCII.GetBytes(computedHash);
        var receivedBytes = Encoding.ASCII.GetBytes(receivedHash);

        if (computedBytes.Length != receivedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(computedBytes, receivedBytes);
    }
}
