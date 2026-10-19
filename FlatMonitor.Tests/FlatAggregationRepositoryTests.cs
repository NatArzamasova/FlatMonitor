using NUnit.Framework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Storage;

namespace workInProgress.Tests;

[TestFixture]
public class FlatAggregationRepositoryTests
{
    private DbContextOptions<FlatsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Test]
    public async Task InitializeAsync_CreatesSchema()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatAggregationRepository(options);

        // Act
        await repo.InitializeAsync();

        // Assert - verify table exists by querying
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatAggregations.CountAsync();
        count.Should().Be(0);
    }

    [Test]
    public async Task RefreshAsync_PopulatesTable_FromFlatsSnapshots()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatAggregationRepository(options);
        await repo.InitializeAsync();

        // Seed FlatsSnapshots and ScrapingHistory
        await using var dbContext = new FlatsDbContext(options);
        var now = DateTime.UtcNow.ToString("O");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create a successful scraping history (no longer used for aggregations)
        var history = new ScrapingHistory
        {
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-10),
            EndTimeUtc = DateTime.UtcNow,
            Status = "Successful",
            FlatsScrapedCount = 2
        };
        dbContext.ScrapingHistories.Add(history);
        await dbContext.SaveChangesAsync();

        var snap1 = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "1",
            District = "Innenstadt",
            RoomsCount = 2,
            PriceEur = 500000,
            PricePerMeter = 5000,
            ScrapedAtUtc = now
        };
        var snap2 = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "2",
            District = "Innenstadt",
            RoomsCount = 2,
            PriceEur = 550000,
            PricePerMeter = 5500,
            ScrapedAtUtc = now
        };

        dbContext.FlatsSnapshots.Add(snap1);
        dbContext.FlatsSnapshots.Add(snap2);

        // Set shadow properties
        dbContext.Entry(snap1).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap1).Property<string>("UpdatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap2).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap2).Property<string>("UpdatedAtUtc").CurrentValue = now;

        await dbContext.SaveChangesAsync();

        // Act
        await repo.RefreshAsync();

        // Assert
        var aggregations = await dbContext.FlatAggregations.ToListAsync();
        aggregations.Should().HaveCount(1);
        aggregations.First().District.Should().Be("Innenstadt");
        aggregations.First().RoomsCount.Should().Be(2);
        aggregations.First().Count.Should().Be(2);
        aggregations.First().ScrapingDate.Should().Be(today);
    }

    [Test]
    public async Task RefreshAsync_ClearsPreviousData_BeforeInserting()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatAggregationRepository(options);
        await repo.InitializeAsync();

        await using var dbContext = new FlatsDbContext(options);

        // Seed old aggregations
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "OldDistrict",
            RoomsCount = 1,
            Count = 10,
            AvgPricePerMeter = 1000m,
            ScrapingDate = today.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        // Seed new snapshots
        var now = DateTime.UtcNow.ToString("O");

        // Create a successful scraping history (no longer used for aggregations)
        var history = new ScrapingHistory
        {
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-10),
            EndTimeUtc = DateTime.UtcNow,
            Status = "Successful",
            FlatsScrapedCount = 1
        };
        dbContext.ScrapingHistories.Add(history);
        await dbContext.SaveChangesAsync();

        var snap = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "1",
            District = "NewDistrict",
            RoomsCount = 2,
            PriceEur = 500000,
            PricePerMeter = 5000,
            ScrapedAtUtc = now
        };
        dbContext.FlatsSnapshots.Add(snap);
        dbContext.Entry(snap).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap).Property<string>("UpdatedAtUtc").CurrentValue = now;
        await dbContext.SaveChangesAsync();

        // Act
        await repo.RefreshAsync();

        // Assert - old data from yesterday should still exist, new today's data should replace today's
        var aggregations = await dbContext.FlatAggregations.ToListAsync();
        aggregations.Should().HaveCount(2);  // One from yesterday, one from today
        aggregations.Where(a => a.ScrapingDate == today).Should().HaveCount(1);
        aggregations.First(a => a.ScrapingDate == today).District.Should().Be("NewDistrict");
        aggregations.Should().Contain(a => a.District == "OldDistrict");
    }

    [Test]
    public async Task RefreshAsync_WithNoMatchingSnapshots_ResultsInEmptyTable()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatAggregationRepository(options);
        await repo.InitializeAsync();

        // Don't seed any matching snapshots (null District would be excluded anyway)

        // Act
        await repo.RefreshAsync();

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatAggregations.CountAsync();
        count.Should().Be(0);
    }

    [Test]
    public async Task RefreshAsync_ExcludesInvalidSnapshots()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatAggregationRepository(options);
        await repo.InitializeAsync();

        await using var dbContext = new FlatsDbContext(options);
        var now = DateTime.UtcNow.ToString("O");

        // Create a successful scraping history (no longer used for aggregations)
        var history = new ScrapingHistory
        {
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-10),
            EndTimeUtc = DateTime.UtcNow,
            Status = "Successful",
            FlatsScrapedCount = 1
        };
        dbContext.ScrapingHistories.Add(history);
        await dbContext.SaveChangesAsync();

        // Seed snapshots: one valid, one with null district, one with zero price
        var snap1 = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "1",
            District = "Innenstadt",
            RoomsCount = 2,
            PriceEur = 500000,
            PricePerMeter = 5000,
            ScrapedAtUtc = now
        };
        var snap2 = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "2",
            District = null,
            RoomsCount = 2,
            PriceEur = 500000,
            PricePerMeter = 5000,
            ScrapedAtUtc = now
        };
        var snap3 = new FlatSnapshot
        {
            Source = "OhneMakler",
            ListingId = "3",
            District = "Innenstadt",
            RoomsCount = 2,
            PriceEur = 0,
            PricePerMeter = null,
            ScrapedAtUtc = now
        };

        dbContext.FlatsSnapshots.Add(snap1);
        dbContext.FlatsSnapshots.Add(snap2);
        dbContext.FlatsSnapshots.Add(snap3);

        // Set shadow properties for all
        dbContext.Entry(snap1).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap1).Property<string>("UpdatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap2).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap2).Property<string>("UpdatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap3).Property<string>("CreatedAtUtc").CurrentValue = now;
        dbContext.Entry(snap3).Property<string>("UpdatedAtUtc").CurrentValue = now;

        await dbContext.SaveChangesAsync();

        // Act
        await repo.RefreshAsync();

        // Assert - only 1 valid aggregation
        var aggregations = await dbContext.FlatAggregations.ToListAsync();
        aggregations.Should().HaveCount(1);
        aggregations.First().Count.Should().Be(1);
    }
}
