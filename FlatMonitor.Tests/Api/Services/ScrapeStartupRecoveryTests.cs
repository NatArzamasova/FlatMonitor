using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Services;

/// <summary>
/// The real repositories on in-memory SQLite. Unless a test says otherwise, FlatsSnapshots holds the "partial" flat of
/// the last run and the backup holds the "before" flat from before that run.
/// </summary>
[TestFixture]
public class ScrapeStartupRecoveryTests
{
    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;
    private FlatSnapshotRepository _snapshots = null!;
    private ListLogger<ScrapeStartupRecovery> _logger = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.Database.EnsureCreatedAsync();
        _snapshots = new FlatSnapshotRepository(_dbContext);
        _logger = new ListLogger<ScrapeStartupRecovery>();

        await _snapshots.UpsertListingsAsync([FakeFlatScraper.CreateFlat("before")]);
        await _snapshots.BackupFlatsSnapshotsAsync();
        await _snapshots.ClearAsync();
        await _snapshots.UpsertListingsAsync([FakeFlatScraper.CreateFlat("partial")]);
        _dbContext.ChangeTracker.Clear();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task RunLeftRunning_IsCanceledAndTheBackupRestored()
    {
        await AddRunAsync(ScrapingStatus.Running);

        await RunRecoveryAsync();

        (await LastRunAsync()).Status.Should().Be(ScrapingStatus.Canceled);
        (await LastRunAsync()).ErrorMessage.Should().Be(ScrapeStartupRecovery.InterruptedRunMessage);
        (await FlatIdsAsync()).Should().Equal("before");
    }

    [TestCase(ScrapingStatus.PartialFailed)]
    [TestCase(ScrapingStatus.Failed)]
    [TestCase(ScrapingStatus.Canceled)]
    public async Task LastRunNotSuccessful_RestoresTheBackup(ScrapingStatus status)
    {
        await AddRunAsync(status);

        await RunRecoveryAsync();

        (await FlatIdsAsync()).Should().Equal("before");
        (await LastRunAsync()).Status.Should().Be(status, "only runs left Running change their status");
        _logger.Messages.Should().Contain(m => m.StartsWith("Restored FlatsSnapshots from the backup"));
    }

    [Test]
    public async Task LastRunSuccessful_KeepsTheTable()
    {
        await AddRunAsync(ScrapingStatus.PartialFailed, hoursAgo: 2);
        await AddRunAsync(ScrapingStatus.Successful, hoursAgo: 1);

        await RunRecoveryAsync();

        (await FlatIdsAsync()).Should().Equal("partial");
    }

    [Test]
    public async Task LastRunNotSuccessfulWithoutBackup_KeepsTheTableAndWarns()
    {
        await _snapshots.DeleteBackupAsync();
        await AddRunAsync(ScrapingStatus.Failed);

        await RunRecoveryAsync();

        (await FlatIdsAsync()).Should().Equal("partial");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
            .Which.Message.Should().Contain("no backup to restore");
    }

    [Test]
    public async Task NoRunsYet_DoesNothing()
    {
        await RunRecoveryAsync();

        (await FlatIdsAsync()).Should().Equal("partial");
        _logger.Entries.Should().BeEmpty();
    }

    private Task RunRecoveryAsync() =>
        new ScrapeStartupRecovery(new ScrapingHistoryRepository(_dbContext), _snapshots, _logger).RunAsync();

    private async Task AddRunAsync(ScrapingStatus status, int hoursAgo = 1)
    {
        _dbContext.ScrapingHistories.Add(new ScrapingHistory { StartTimeUtc = DateTime.UtcNow.AddHours(-hoursAgo), Status = status });
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

    private FlatsDbContext CreateReadContext() =>
        new(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
}
