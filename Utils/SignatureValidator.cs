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
    /// 長さが異なる場合もダミー比較を行い、長さ情報をタイミングで漏洩しない。
    /// </summary>
    public static bool Validate(byte[] rawBody, IHeaderDictionary headers, string secret)
    {
        var signatureHeader = headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        // クライアント実装差（大文字 HEX など）を吸収するため小文字に正規化する
        var receivedHash = signatureHeader[SignaturePrefix.Length..].ToLowerInvariant();

        using var hmac        = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var       computedHash = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();

        var computedBytes = Encoding.ASCII.GetBytes(computedHash);
        var receivedBytes = Encoding.ASCII.GetBytes(receivedHash);

        // 長さが異なる場合もタイミング情報を漏洩しないよう、常に FixedTimeEquals を実行する。
        // receivedBytes を computedBytes と同じ長さに正規化してから比較する。
        var normalizedReceived = new byte[computedBytes.Length];
        var copyLen = Math.Min(receivedBytes.Length, normalizedReceived.Length);
        receivedBytes.AsSpan(0, copyLen).CopyTo(normalizedReceived);

        var equal = CryptographicOperations.FixedTimeEquals(computedBytes, normalizedReceived);

        // 長さが違う場合は false（正規化で内容が変わっているため）
        return equal && receivedBytes.Length == computedBytes.Length;
    }
}
