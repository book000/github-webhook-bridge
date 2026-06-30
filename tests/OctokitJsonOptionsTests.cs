using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

public class OctokitJsonOptionsTests
{
    [Fact]
    public void Value_PropertyNameCaseInsensitiveIsTrue()
        => Assert.True(OctokitJsonOptions.Value.PropertyNameCaseInsensitive);

    [Fact]
    public void Value_AllowTrailingCommasIsTrue()
        => Assert.True(OctokitJsonOptions.Value.AllowTrailingCommas);

    [Fact]
    public void Value_UnknownFieldsAreIgnored()
    {
        var json = """{"known":"hello","unknown_field":42}""";
        var result = JsonSerializer.Deserialize<KnownOnly>(json, OctokitJsonOptions.Value);
        Assert.Equal("hello", result!.Known);
    }

    [Fact]
    public void Value_IsReadOnly()
        => Assert.Throws<InvalidOperationException>(
            () => OctokitJsonOptions.Value.AllowTrailingCommas = false);

    private sealed record KnownOnly([property: JsonPropertyName("known")] string Known);
}
