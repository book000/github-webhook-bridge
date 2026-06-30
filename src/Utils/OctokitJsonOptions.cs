using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// Octokit.Webhooks モデルのデシリアライズに使用する共有 <see cref="JsonSerializerOptions"/>。
/// </summary>
internal static class OctokitJsonOptions
{
    /// <summary>読み取り専用の共有オプションインスタンス。</summary>
    public static readonly JsonSerializerOptions Value = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        opts.MakeReadOnly();
        return opts;
    }
}
