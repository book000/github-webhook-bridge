using Azure;
using Azure.Data.Tables;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>Tests for the expired-row query used by <see cref="MessageCacheService.DeleteExpiredAsync"/>.</summary>
public class MessageCacheServicePurgeTests
{
    /// <summary>The filter selects rows whose Timestamp is strictly older than the cutoff, in UTC.</summary>
    [Fact]
    public void ExpiredFilterSelectsRowsOlderThanTheCutoff()
        => Assert.Equal(
            "Timestamp lt datetime'2026-01-02T03:04:05.0000000Z'",
            MessageCacheService.BuildExpiredFilter(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));

    /// <summary>A cutoff with a non-UTC offset is converted to UTC before it is written into the filter.</summary>
    [Fact]
    public void ExpiredFilterConvertsTheCutoffToUtc()
        => Assert.Equal(
            "Timestamp lt datetime'2026-01-02T03:04:05.0000000Z'",
            MessageCacheService.BuildExpiredFilter(new DateTimeOffset(2026, 1, 2, 12, 4, 5, TimeSpan.FromHours(9))));

    /// <summary>One partition with 250 rows is deleted in transactions of 100, 100 and 50.</summary>
    [Fact]
    public async Task DeleteExpiredChunksAPartitionIntoTransactionsOf100()
    {
        var sizes = new List<int>();
        var table = TableReturning(Page(Rows("p1", 250)));
        table.Setup(t => t.SubmitTransactionAsync(It.IsAny<IEnumerable<TableTransactionAction>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<TableTransactionAction>, CancellationToken>((actions, _) => sizes.Add(actions.Count()))
            .ReturnsAsync(Mock.Of<Response<IReadOnlyList<Response>>>());

        var deleted = await CreateService(table).DeleteExpiredAsync(CancellationToken.None);

        Assert.Equal(250, deleted);
        Assert.Equal([100, 100, 50], sizes);
    }

    /// <summary>Rows of different partitions never share a transaction, across all pages.</summary>
    [Fact]
    public async Task DeleteExpiredUsesOneTransactionPerPartition()
    {
        var partitions = new List<string[]>();
        var table = TableReturning(Page([.. Rows("p1", 2), .. Rows("p2", 3)]), Page(Rows("p1", 1)));
        table.Setup(t => t.SubmitTransactionAsync(It.IsAny<IEnumerable<TableTransactionAction>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<TableTransactionAction>, CancellationToken>(
                (actions, _) => partitions.Add([.. actions.Select(a => a.Entity.PartitionKey).Distinct()]))
            .ReturnsAsync(Mock.Of<Response<IReadOnlyList<Response>>>());

        var deleted = await CreateService(table).DeleteExpiredAsync(CancellationToken.None);

        Assert.Equal(6, deleted);
        Assert.Equal(3, partitions.Count);
        Assert.All(partitions, p => Assert.Single(p));
    }

    /// <summary>A 404 on the transaction retries row by row and does not count rows that are already gone.</summary>
    [Fact]
    public async Task DeleteExpiredFallsBackToSingleDeletesOnNotFound()
    {
        var table = TableReturning(Page(Rows("p1", 3)));
        table.Setup(t => t.SubmitTransactionAsync(It.IsAny<IEnumerable<TableTransactionAction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "gone"));
        table.Setup(t => t.DeleteEntityAsync("p1", "r0", ETag.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        table.Setup(t => t.DeleteEntityAsync("p1", "r1", ETag.All, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "gone"));
        table.Setup(t => t.DeleteEntityAsync("p1", "r2", ETag.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var deleted = await CreateService(table).DeleteExpiredAsync(CancellationToken.None);

        Assert.Equal(2, deleted);
    }

    /// <summary>A failure other than 404 is not swallowed.</summary>
    [Fact]
    public async Task DeleteExpiredPropagatesOtherFailures()
    {
        var table = TableReturning(Page(Rows("p1", 1)));
        table.Setup(t => t.SubmitTransactionAsync(It.IsAny<IEnumerable<TableTransactionAction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "busy"));

        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => CreateService(table).DeleteExpiredAsync(CancellationToken.None));

        Assert.Equal(503, ex.Status);
    }

    private static MessageCacheService CreateService(Mock<TableClient> table)
        => new(table.Object, NullLogger<MessageCacheService>.Instance);

    private static List<TableEntity> Rows(string partition, int count)
        => [.. Enumerable.Range(0, count).Select(i => new TableEntity(partition, $"r{i}"))];

    private static Page<TableEntity> Page(List<TableEntity> rows)
        => Azure.Page<TableEntity>.FromValues(rows, null, Mock.Of<Response>());

    private static Mock<TableClient> TableReturning(params Page<TableEntity>[] pages)
    {
        var table = new Mock<TableClient>();
        table.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(pages));
        return table;
    }
}
