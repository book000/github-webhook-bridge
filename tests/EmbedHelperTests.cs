using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests that <see cref="EmbedHelper.CreateEmbed"/> keeps every embed within Discord's documented limits.</summary>
public class EmbedHelperTests
{
    private const int TotalLimit = 6000;

    private static int TotalLength(DiscordEmbed embed)
        => (embed.Title?.Length ?? 0)
            + (embed.Description?.Length ?? 0)
            + (embed.Footer?.Text.Length ?? 0)
            + (embed.Author?.Name.Length ?? 0)
            + (embed.Fields?.Sum(f => f.Name.Length + f.Value.Length) ?? 0);

    private static List<DiscordEmbedField> Fields(int count, int nameLength, int valueLength)
        => [.. Enumerable.Range(0, count).Select(i => new DiscordEmbedField(new string('n', nameLength), new string('v', valueLength), true))];

    [Fact]
    public void Truncate_WithinLimit_ReturnsSameValue()
        => Assert.Equal("abc", EmbedHelper.Truncate("abc", 3));

    [Fact]
    public void Truncate_OverLimit_EndsWithEllipsisAtExactLimit()
    {
        var result = EmbedHelper.Truncate(new string('a', 300), 256);

        Assert.Equal(256, result.Length);
        Assert.Equal(new string('a', 255) + "…", result);
    }

    [Fact]
    public void Truncate_CutInsideSurrogatePair_DropsLoneHighSurrogate()
    {
        // 254 'a' + U+1F600 (2 UTF-16 units) + 'b': the cut at 255 units lands between the surrogates.
        var value = new string('a', 254) + "\U0001F600" + "b";

        var result = EmbedHelper.Truncate(value, 256);

        Assert.Equal(new string('a', 254) + "…", result);
    }

    [Fact]
    public void CreateEmbed_InLimitInput_IsUnchanged()
    {
        List<DiscordEmbedField> fields = [new("name", "value", true)];

        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            "push", 1, "title", "description", author: new DiscordEmbedAuthor("login"), fields: fields);

        Assert.Equal("title", embed.Title);
        Assert.Equal("description", embed.Description);
        Assert.Equal("login", embed.Author?.Name);
        Assert.Equal(fields, embed.Fields);
    }

    [Fact]
    public void CreateEmbed_NullTitle_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>("title", () => EmbedHelper.CreateEmbed("push", 1, null!));

    [Fact]
    public void CreateEmbed_NullDescriptionAndFields_StayNull()
    {
        DiscordEmbed embed = EmbedHelper.CreateEmbed("push", 1, "title");

        Assert.Null(embed.Description);
        Assert.Null(embed.Fields);
    }

    [Fact]
    public void CreateEmbed_OverLimitParts_AreTruncatedToEachLimit()
    {
        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            "push",
            1,
            new string('t', 300),
            new string('d', 5000),
            author: new DiscordEmbedAuthor(new string('a', 300)),
            fields: Fields(3, 300, 1100));

        Assert.Equal(256, embed.Title?.Length);
        Assert.EndsWith("…", embed.Title, StringComparison.Ordinal);
        Assert.Equal(256, embed.Author?.Name.Length);
        Assert.Equal(3, embed.Fields?.Count);
        Assert.All(embed.Fields!, f =>
        {
            Assert.Equal(256, f.Name.Length);
            Assert.Equal(1024, f.Value.Length);
        });
        Assert.True(embed.Description!.Length <= 4096);
        Assert.True(TotalLength(embed) <= TotalLimit);
    }

    [Fact]
    public void CreateEmbed_MoreThan25Fields_KeepsFirst25()
    {
        List<DiscordEmbedField> fields = [.. Enumerable.Range(0, 30).Select(i => new DiscordEmbedField($"n{i}", "v", true))];

        DiscordEmbed embed = EmbedHelper.CreateEmbed("push", 1, "title", fields: fields);

        Assert.Equal(fields.Take(25), embed.Fields);
    }

    [Fact]
    public void CreateEmbed_OverLimitFooter_IsTruncatedTo2048()
    {
        DiscordEmbed embed = EmbedHelper.CreateEmbed(new string('e', 3000), 1, "title");

        Assert.Equal(2048, embed.Footer?.Text.Length);
    }

    [Fact]
    public void CreateEmbed_TotalOverLimit_ShrinksDescriptionBeforeDroppingFields()
    {
        // 3 fields total 3840, so title + description + fields + footer exceed 6000 but the fields alone fit.
        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            "push", 1, new string('t', 256), new string('d', 4096), fields: Fields(3, 256, 1024));

        Assert.Equal(3, embed.Fields?.Count);
        Assert.True(embed.Description!.Length < 4096);
        Assert.Equal(TotalLimit, TotalLength(embed));
    }

    [Fact]
    public void CreateEmbed_FieldsAloneOverTotal_DropTrailingFields()
    {
        DiscordEmbed embed = EmbedHelper.CreateEmbed(
            "push", 1, new string('t', 256), new string('d', 4096), fields: Fields(10, 256, 1024));

        Assert.Equal(string.Empty, embed.Description);
        Assert.True(embed.Fields!.Count < 10);
        Assert.True(TotalLength(embed) <= TotalLimit);
    }
}
