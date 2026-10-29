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

    [Test]
    public async Task StartProgressComplete_RecordTheWholeRun()
    {
        // Arrange
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateSqliteContextAsync(connection);
        var repository = new ScrapingHistoryRepository(context);

        // Act
        var runId = await repository.StartAsync();
        var running = await context.ScrapingHistories.AsNoTracking().SingleAsync();
        await repository.AddProgressAsync(runId, 20);
        await repository.AddProgressAsync(runId, 5);
        await repository.CompleteAsync(runId, ScrapingStatus.PartialFailed, "ImmobilienScout24: Login failed");

        // Assert
        running.Status.Should().Be(ScrapingStatus.Running);
        running.StartTimeUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        running.EndTimeUtc.Should().BeNull();
        var run = await context.ScrapingHistories.AsNoTracking().SingleAsync();
        run.Id.Should().Be(runId);
        run.FlatsScrapedCount.Should().Be(25);
        run.Status.Should().Be(ScrapingStatus.PartialFailed);
        run.ErrorMessage.Should().Be("ImmobilienScout24: Login failed");
        run.EndTimeUtc.Should().NotBeNull().And.BeOnOrAfter(run.StartTimeUtc);
    }

    [Test]
    public async Task ProgressAndComplete_DoNotSaveOtherPendingChangesOfTheSharedContext()
    {
        // Arrange: a failed batch left an unsaved aggregation row in the shared context
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateSqliteContextAsync(connection);
        var repository = new ScrapingHistoryRepository(context);
        var runId = await repository.StartAsync();
        context.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            ScrapingDate = DateOnly.FromDateTime(DateTime.UtcNow), District = "Oststadt", RoomsCount = 2, Count = 1, AvgPricePerMeter = 5000m,
        });

        // Act
        await repository.AddProgressAsync(runId, 1);
        await repository.CompleteAsync(runId, ScrapingStatus.Failed, "Disk full");

        // Assert
        (await context.FlatAggregations.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.ScrapingHistories.AsNoTracking().SingleAsync()).Status.Should().Be(ScrapingStatus.Failed);
    }

    [Test]
    public async Task StartAsync_DoesNotKeepTheRunTracked()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateSqliteContextAsync(connection);

        await new ScrapingHistoryRepository(context).StartAsync();

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    // ExecuteUpdate needs a relational provider
    private static async Task<FlatsDbContext> CreateSqliteContextAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var context = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
