using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

/// <summary>
/// Migrates an in-memory SQLite database up to the migration before AddFlatsSnapshotsStaging, writes a flat and the
/// backup table the old backup → clear → restore scrape left behind, then applies the rest.
/// </summary>
[TestFixture]
public class AddFlatsSnapshotsStagingMigrationTests
{
    private const string BeforeStaging = "20261029084644_ModelCleanup";

    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.GetService<IMigrator>().MigrateAsync(BeforeStaging);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO FlatsSnapshots (Source, ListingId, Url, Address, ScrapedAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('OhneMakler', '42', 'https://example.org/42', 'Address',
                    '2026-10-29 08:07:48.897', '2026-10-29 08:44:28.994', '2026-10-29 08:44:28.994');
            CREATE TABLE FlatsSnapshots_Backup AS SELECT * FROM FlatsSnapshots;
            """);

        await _dbContext.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task PublishedFlats_AreKept()
    {
        (await _dbContext.FlatsSnapshots.SingleAsync()).ListingId.Should().Be("42");
    }

    [Test]
    public async Task StagingTable_ExistsAndIsEmpty()
    {
        (await _dbContext.FlatsSnapshotsStaging.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task OldBackupTable_IsDropped()
    {
        var tables = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'FlatsSnapshots_Backup'")
            .SingleAsync();

        tables.Should().Be(0);
    }

    [Test]
    public async Task ARun_PublishesIntoTheMigratedTable()
    {
        var repository = new FlatSnapshotRepository(_dbContext);
        await repository.BeginRunAsync();
        await repository.AddToRunAsync([new FlatMonitor.Models.Flat
        {
            Source = "OhneMakler",
            ListingId = "42",
            Url = "https://example.org/42",
            Address = "Address",
            PriceEur = 300000,
            ScrapedAtUtc = DateTime.UtcNow,
        }]);
        await repository.PublishSourceAsync("OhneMakler");

        await using var readContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        var row = await readContext.FlatsSnapshots.SingleAsync();
        row.PriceEur.Should().Be(300000);
        readContext.Entry(row).Property<DateTime>("CreatedAtUtc").CurrentValue
            .Should().BeCloseTo(new DateTime(2026, 10, 29, 8, 44, 28, 994, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
    }
}
