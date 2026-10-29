using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class ScrapingHistoryRepositoryTests
{
    [Test]
    public async Task GetLatestAsync_ReturnsNewestHistoryAndNewestSuccessfulHistory()
    {
        var options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase($"ScrapingHistory_{Guid.NewGuid()}")
            .Options;
        await using var context = new FlatsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var baseline = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        context.ScrapingHistories.AddRange(
            new ScrapingHistory { StartTimeUtc = baseline, Status = ScrapingStatus.Successful, FlatsScrapedCount = 3 },
            new ScrapingHistory { StartTimeUtc = baseline.AddHours(1), Status = ScrapingStatus.Failed, ErrorMessage = "network", FlatsScrapedCount = 1 },
            new ScrapingHistory { StartTimeUtc = baseline.AddHours(2), Status = ScrapingStatus.Successful, FlatsScrapedCount = 5 });
        await context.SaveChangesAsync();
        var repository = new ScrapingHistoryRepository(context);

        var result = await repository.GetLatestAsync();

        result.Latest.Should().NotBeNull();
        result.Latest!.StartTimeUtc.Should().Be(baseline.AddHours(2));
        result.Latest.Status.Should().Be(ScrapingStatus.Successful);
        result.Latest.FlatsScrapedCount.Should().Be(5);
        result.LatestSuccessful.Should().NotBeNull();
        result.LatestSuccessful!.StartTimeUtc.Should().Be(baseline.AddHours(2));
        result.LatestSuccessful.FlatsScrapedCount.Should().Be(5);
    }

    [Test]
    public async Task GetLatestAsync_ReturnsNullHistoriesWhenThereAreNoRows()
    {
        var options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase($"ScrapingHistory_{Guid.NewGuid()}")
            .Options;
        await using var context = new FlatsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new ScrapingHistoryRepository(context);

        var result = await repository.GetLatestAsync();

        result.Latest.Should().BeNull();
        result.LatestSuccessful.Should().BeNull();
    }

    [Test]
    public async Task CancelUnfinishedRunsAsync_CancelsOnlyRunningRuns()
    {
        // ExecuteUpdate needs a relational provider
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var start = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);
        context.ScrapingHistories.AddRange(
            new ScrapingHistory { StartTimeUtc = start, EndTimeUtc = start.AddMinutes(20), Status = ScrapingStatus.Successful, FlatsScrapedCount = 575 },
            new ScrapingHistory { StartTimeUtc = start.AddHours(1), Status = ScrapingStatus.Running, FlatsScrapedCount = 121 },
            new ScrapingHistory { StartTimeUtc = start.AddHours(2), EndTimeUtc = start.AddHours(2), Status = ScrapingStatus.Failed, ErrorMessage = "network" },
            new ScrapingHistory { StartTimeUtc = start.AddHours(3), Status = ScrapingStatus.Running });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var canceled = await new ScrapingHistoryRepository(context).CancelUnfinishedRunsAsync("Interrupted");

        canceled.Should().Be(2);
        var runs = await context.ScrapingHistories.AsNoTracking().OrderBy(h => h.Id).ToListAsync();
        runs.Select(h => h.Status).Should().Equal(
            ScrapingStatus.Successful, ScrapingStatus.Canceled, ScrapingStatus.Failed, ScrapingStatus.Canceled);
        runs[1].ErrorMessage.Should().Be("Interrupted");
        runs[1].EndTimeUtc.Should().BeNull("the real end time is unknown");
        runs[1].FlatsScrapedCount.Should().Be(121);
        runs[2].ErrorMessage.Should().Be("network");
    }
}
