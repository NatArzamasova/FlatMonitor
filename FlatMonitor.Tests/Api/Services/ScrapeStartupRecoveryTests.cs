using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Services;

/// <summary>
/// The real repositories on in-memory SQLite. FlatsSnapshots holds the "published" flat of an earlier run; the
/// staging table holds the "staged" flat an interrupted run left behind.
/// </summary>
[TestFixture]
public class ScrapeStartupRecoveryTests
{
    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;
    private FlatRepository _flats = null!;
    private ListLogger<ScrapeStartupRecovery> _logger = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.Database.EnsureCreatedAsync();
        _flats = new FlatRepository(_dbContext);
        _logger = new ListLogger<ScrapeStartupRecovery>();

        await _flats.AddToRunAsync([FakeFlatScraper.CreateFlat("published")]);
        await _flats.PublishSourceAsync("OhneMakler");
        await _flats.BeginRunAsync();
        await _flats.AddToRunAsync([FakeFlatScraper.CreateFlat("staged")]);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task RunLeftRunning_IsCanceledAndItsStagedFlatsDropped()
    {
        await AddRunAsync(ScrapingStatus.Running);

        await RunRecoveryAsync();

        (await LastRunAsync()).Status.Should().Be(ScrapingStatus.Canceled);
        (await LastRunAsync()).ErrorMessage.Should().Be(ScrapeStartupRecovery.InterruptedRunMessage);
        (await StagedIdsAsync()).Should().BeEmpty();
        (await FlatIdsAsync()).Should().Equal("published");
        _logger.Messages.Should().ContainSingle().Which.Should().StartWith("Marked 1 scrape run(s) left running");
    }

    [TestCase(ScrapingStatus.Successful)]
    [TestCase(ScrapingStatus.PartialFailed)]
    [TestCase(ScrapingStatus.Failed)]
    [TestCase(ScrapingStatus.Canceled)]
    public async Task FinishedLastRun_KeepsThePublishedFlatsAndItsStatus(ScrapingStatus status)
    {
        // A finished run only ever replaced whole sources, so there is nothing to restore
        await AddRunAsync(status);

        await RunRecoveryAsync();

        (await FlatIdsAsync()).Should().Equal("published");
        (await LastRunAsync()).Status.Should().Be(status);
        _logger.Entries.Should().BeEmpty();
    }

    [Test]
    public async Task NoRunsYet_OnlyEmptiesTheStagingTable()
    {
        await RunRecoveryAsync();

        (await StagedIdsAsync()).Should().BeEmpty();
        (await FlatIdsAsync()).Should().Equal("published");
        _logger.Entries.Should().BeEmpty();
    }

    private Task RunRecoveryAsync() =>
        new ScrapeStartupRecovery(new ScrapingHistoryRepository(_dbContext), _flats, _logger).RunAsync();

    private async Task AddRunAsync(ScrapingStatus status)
    {
        _dbContext.ScrapingHistories.Add(new ScrapingHistory { StartTimeUtc = DateTime.UtcNow.AddHours(-1), Status = status });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<ScrapingHistory> LastRunAsync()
    {
        await using var dbContext = CreateReadContext();
        return await dbContext.ScrapingHistories.OrderByDescending(h => h.StartTimeUtc).FirstAsync();
    }

    private async Task<List<string>> FlatIdsAsync()
    {
        await using var dbContext = CreateReadContext();
        return await dbContext.FlatsSnapshots.Select(s => s.ListingId).OrderBy(id => id).ToListAsync();
    }

    private async Task<List<string>> StagedIdsAsync()
    {
        await using var dbContext = CreateReadContext();
        return await dbContext.FlatsSnapshotsStaging.Select(s => s.ListingId).ToListAsync();
    }

    private FlatsDbContext CreateReadContext() =>
        new(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
}
