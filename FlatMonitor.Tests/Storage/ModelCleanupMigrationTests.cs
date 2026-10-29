using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

/// <summary>
/// Migrates an in-memory SQLite database up to the migration before ModelCleanup, writes rows the way the old code did
/// (round-trip date strings, title and commission in the columns the FlatDetails owned type used), then applies ModelCleanup.
/// </summary>
[TestFixture]
public class ModelCleanupMigrationTests
{
    private const string BeforeCleanup = "20261028104827_AddFilterPresets";

    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.GetService<IMigrator>().MigrateAsync(BeforeCleanup);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO FlatsSnapshots (Source, ListingId, Url, Address, Title, CommissionPercent, PriceEur, PricePerMeter,
                                        ScrapedAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('ImmobilienScout24', '42', 'https://example.org/42', 'Address', 'Old title', 3.57, 300000, 4000,
                    '2026-10-29T08:07:48.8973376Z', '2026-10-29T08:44:28.9944471Z', '2026-10-29T08:44:28.9944471Z');
            INSERT INTO ScrapingHistories (StartTimeUtc, Status, FlatsScrapedCount)
            VALUES ('2026-10-29 08:00:00', 'PartialFailed', 1);
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
    public async Task ExistingDates_ReadBackAsTheSameUtcInstant()
    {
        await using var dbContext = CreateContext();
        var snapshot = await dbContext.FlatsSnapshots.SingleAsync();

        snapshot.ScrapedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        snapshot.ScrapedAtUtc.Should().BeCloseTo(new DateTime(2026, 10, 29, 8, 7, 48, 897, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        dbContext.Entry(snapshot).Property<DateTime>("CreatedAtUtc").CurrentValue
            .Should().BeCloseTo(new DateTime(2026, 10, 29, 8, 44, 28, 994, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task ExistingTitleAndCommission_StayReadable()
    {
        await using var dbContext = CreateContext();
        var snapshot = await dbContext.FlatsSnapshots.SingleAsync();

        snapshot.Title.Should().Be("Old title");
        snapshot.CommissionPercent.Should().Be(3.57);
    }

    [Test]
    public async Task ExistingStatus_ReadsAsEnum()
    {
        await using var dbContext = CreateContext();

        (await dbContext.ScrapingHistories.SingleAsync()).Status.Should().Be(ScrapingStatus.PartialFailed);
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
    public async Task NewRows_SortTogetherWithMigratedRows()
    {
        await using (var dbContext = CreateContext())
        {
            var repository = new FlatSnapshotRepository(dbContext);
            await repository.AddToRunAsync([new Flat
            {
                Source = "OhneMakler",
                ListingId = "1",
                Url = "https://example.org/1",
                ScrapedAtUtc = new DateTime(2026, 10, 29, 8, 7, 49, DateTimeKind.Utc),
            }]);
            await repository.PublishSourceAsync("OhneMakler");
        }

        await using var readContext = CreateContext();
        var ordered = await readContext.FlatsSnapshots.OrderByDescending(s => s.ScrapedAtUtc).Select(s => s.ListingId).ToListAsync();

        ordered.Should().Equal("1", "42");
    }

    private FlatsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
}
