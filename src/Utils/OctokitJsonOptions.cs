using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// Class that provides the shared <see cref="JsonSerializerOptions"/> used to deserialize Octokit.Webhooks models
/// </summary>
internal static class OctokitJsonOptions
{
    /// <summary>Holds the read-only shared options instance</summary>
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
