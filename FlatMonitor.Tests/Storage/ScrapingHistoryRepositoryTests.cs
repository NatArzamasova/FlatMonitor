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
}
