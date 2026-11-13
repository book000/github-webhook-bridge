using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for <see cref="TextHelper.Truncate"/>.</summary>
public class TextHelperTests
{
    [Fact]
    public void TruncateShortValueIsUnchanged()
        => Assert.Equal("hello", TextHelper.Truncate("hello", 500));

    [Fact]
    public void TruncateExactLengthValueIsUnchanged()
    {
        var value = new string('a', 500);
        Assert.Equal(value, TextHelper.Truncate(value, 500));
    }

    [Fact]
    public void TruncateLongValueKeepsMaxLengthAndAppendsEllipsis()
        => Assert.Equal(new string('a', 500) + "...", TextHelper.Truncate(new string('a', 600), 500));

    [Fact]
    public void TruncateDropsHighSurrogateAtCutBoundary()
    {
        var value = new string('a', 499) + "😀" + new string('b', 50);

        var result = TextHelper.Truncate(value, 500);

        Assert.Equal(new string('a', 499) + "...", result);
    }

    [Fact]
    public void TruncateKeepsCompleteSurrogatePairBeforeBoundary()
    {
        var value = new string('a', 498) + "😀" + new string('b', 50);

        var result = TextHelper.Truncate(value, 500);

        Assert.Equal(new string('a', 498) + "😀...", result);
    }

    [Fact]
    public void TruncateClosesFenceOpenedBeforeCut()
    {
        var value = "```csharp\n" + new string('a', 600) + "\n```";

        var result = TextHelper.Truncate(value, 100);

        Assert.Equal("```csharp\n" + new string('a', 90) + "...\n```", result);
    }

    [Fact]
    public void TruncateDoesNotAddFenceWhenFenceIsAlreadyClosed()
    {
        var value = "```\ncode\n```\n" + new string('a', 600);

        var result = TextHelper.Truncate(value, 100);

        Assert.Equal(value[..100] + "...", result);
    }

    [Fact]
    public void TruncateIgnoresIndentedBackticksBeyondThreeSpaces()
    {
        var value = "    ```\n" + new string('a', 600);

        var result = TextHelper.Truncate(value, 100);

        Assert.Equal(value[..100] + "...", result);
    }
}
