using GitHubWebhookBridge.Services;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for the Azure Table RowKey derivation in <see cref="MessageCacheService"/>.</summary>
public class MessageCacheServiceRowKeyTests
{
    private const int MaxRowKeyLength = 512;

    /// <summary>A key whose escaped form fits the limit is only escaped, so existing cache rows stay valid.</summary>
    [Theory]
    [InlineData("owner/repo:refs/heads/main")]
    [InlineData("owner/repo#12-open")]
    public void ShortKeyIsReturnedEscapedAndUnchanged(string key)
        => Assert.Equal(Uri.EscapeDataString(key), MessageCacheService.SanitizeRowKey(key));

    /// <summary>A key whose escaped form is exactly 512 characters is not hashed.</summary>
    [Fact]
    public void KeyAtTheLimitIsNotHashed()
    {
        var key = new string('a', MaxRowKeyLength);

        Assert.Equal(key, MessageCacheService.SanitizeRowKey(key));
    }

    /// <summary>Distinct long keys that share a long prefix get distinct row keys within the limit.</summary>
    [Fact]
    public void LongKeysSharingAPrefixDoNotCollide()
    {
        var prefix = new string('a', 600);

        var first = MessageCacheService.SanitizeRowKey(prefix + "-one");
        var second = MessageCacheService.SanitizeRowKey(prefix + "-two");

        Assert.NotEqual(first, second);
        Assert.True(first.Length <= MaxRowKeyLength);
        Assert.True(second.Length <= MaxRowKeyLength);
    }

    /// <summary>The same long key always maps to the same row key.</summary>
    [Fact]
    public void LongKeyHashIsDeterministic()
    {
        var key = new string('b', 700);

        Assert.Equal(MessageCacheService.SanitizeRowKey(key), MessageCacheService.SanitizeRowKey(key));
    }

    /// <summary>A hashed key is the prefix, the <c>!</c> marker and 32 hex characters.</summary>
    [Fact]
    public void HashedKeyHasMarkerAndHashSuffix()
    {
        var result = MessageCacheService.SanitizeRowKey(new string('c', 700));

        Assert.Equal(MaxRowKeyLength, result.Length);
        Assert.Matches("^c{479}![0-9a-f]{32}$", result);
    }

    /// <summary>The prefix cut never leaves a dangling or partial %XX triplet in front of the marker.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CutNeverSplitsAnEscapedTriplet(int leadingAsciiChars)
    {
        // Each "é" escapes to six characters (%C3%A9), so varying the lead shifts where the cut lands.
        var key = new string('a', leadingAsciiChars) + string.Concat(Enumerable.Repeat("é", 200));

        var result = MessageCacheService.SanitizeRowKey(key);

        var prefix = result[..result.LastIndexOf('!')];
        Assert.True(result.Length <= MaxRowKeyLength);
        Assert.Matches("^(?:[A-Za-z0-9]|%[0-9A-F]{2})*$", prefix);
    }

    /// <summary>An unhashed key never contains the hash marker, even when it contains a tilde or hex-looking text.</summary>
    [Fact]
    public void UnhashedKeyNeverContainsTheMarker()
    {
        var key = "~" + new string('a', 32) + "!";

        var result = MessageCacheService.SanitizeRowKey(key);

        Assert.Equal(Uri.EscapeDataString(key), result);
        Assert.DoesNotContain("!", result, StringComparison.Ordinal);
    }

    /// <summary>The row key never contains a character Azure Table Storage forbids.</summary>
    [Fact]
    public void RowKeyContainsNoForbiddenCharacters()
    {
        var result = MessageCacheService.SanitizeRowKey("a/b\\c#d?e" + new string('/', 600));

        Assert.DoesNotContain(result, c => c is '/' or '\\' or '#' or '?' || char.IsControl(c));
    }
}
