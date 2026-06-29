using System.Diagnostics.CodeAnalysis;
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
    /// <param name="rawBody">検証対象の HTTP リクエストボディバイト列。</param>
    /// <param name="headers">HTTP リクエストヘッダーコレクション。</param>
    /// <param name="secret">HMAC-SHA256 署名計算に使用するシークレット文字列。</param>
    /// <returns>署名が有効な場合は true、無効または署名ヘッダーが存在しない場合は false。</returns>
    [SuppressMessage("Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "GitHub Webhook の署名は仕様上小文字 hex のため ToLowerInvariant が正しい")]
    public static bool Validate(byte[] rawBody, IHeaderDictionary headers, string secret)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var signatureHeader = headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // クライアント実装差（大文字 HEX など）を吸収するため小文字に正規化する
        var receivedHashHex = signatureHeader[SignaturePrefix.Length..].ToLowerInvariant();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHashHex = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();

        var computedHashBytes = Encoding.ASCII.GetBytes(computedHashHex);
        var receivedHashBytes = Encoding.ASCII.GetBytes(receivedHashHex);

        // 長さが異なる場合もタイミング情報を漏洩しないよう、常に FixedTimeEquals を実行する。
        // receivedHashBytes を computedHashBytes と同じ長さに正規化してから比較する。
        var normalizedReceived = new byte[computedHashBytes.Length];
        var copyLength = Math.Min(receivedHashBytes.Length, normalizedReceived.Length);
        receivedHashBytes.AsSpan(0, copyLength).CopyTo(normalizedReceived);

        var equal = CryptographicOperations.FixedTimeEquals(computedHashBytes, normalizedReceived);

        // 長さが違う場合は false（正規化で内容が変わっているため）
        return equal && receivedHashBytes.Length == computedHashBytes.Length;
    }
}
