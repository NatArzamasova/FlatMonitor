using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

/// <summary>
/// Migrates an in-memory SQLite database up to the migration before AddFlatsArchive, writes runs, flats and a preview,
/// then applies the rest: the archive must start with today's flats, each with the run that published it.
/// </summary>
[TestFixture]
public class AddFlatsArchiveMigrationTests
{
    private const string BeforeArchive = "20261104082204_AddScrapingHistorySources";

    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.GetService<IMigrator>().MigrateAsync(BeforeArchive);

        // Runs of every kind may have published sources (a canceled run keeps those published before it), and a run
        // started at a whole second compares as text with flats scraped within it
        await _dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO ScrapingHistories (Id, StartTimeUtc, EndTimeUtc, Status, FlatsScrapedCount, Sources) VALUES
                (1, '2026-11-03 08:00:00.1234567', '2026-11-03 09:00:00', 'Successful', 2, 'OhneMakler'),
                (2, '2026-11-04 10:00:00', '2026-11-04 10:20:00', 'Canceled', 1, 'OhneMakler,Immowelt'),
                (3, '2026-11-05 12:00:00.5', '2026-11-05 13:00:00', 'PartialFailed', 1, 'ImmobilienScout24');

            INSERT INTO FlatsSnapshots (Source, ListingId, Url, Address, PriceEur, Heating, ScrapedAtUtc, CreatedAtUtc, UpdatedAtUtc) VALUES
                ('OhneMakler', 'om-1', 'https://example.org/om-1', 'A', 300000, 'Gas',
                 '2026-11-03 08:05:00.5', '2026-10-29 12:54:24.5656229', '2026-11-03 08:30:00'),
                ('Immowelt', 'iw-1', 'https://example.org/iw-1', 'B', 250000, NULL,
                 '2026-11-04 10:00:00.75', '2026-11-04 10:15:00', '2026-11-04 10:15:00'),
                ('ImmobilienScout24', 'is-1', 'https://example.org/is-1', 'C', 400000, NULL,
                 '2026-11-05 12:30:00', '2026-11-02 09:00:00', '2026-11-05 12:40:00'),
                ('OhneMakler', 'om-old', 'https://example.org/om-old', 'D', 100000, NULL,
                 '2026-11-01 07:00:00', '2026-11-01 07:00:00', '2026-11-01 07:00:00');

            INSERT INTO FlatPreviews (Source, ListingId, ImageUrl, ContentType, Data, DownloadedAtUtc) VALUES
                ('OhneMakler', 'om-1', 'https://example.org/om-1.webp', 'image/webp', X'010203', '2026-11-03 08:06:00');
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
    public async Task EachFlat_IsArchivedWithTheLatestRunStartedBeforeItWasScraped()
    {
        var runs = await _dbContext.FlatsArchive.ToDictionaryAsync(a => a.ListingId, a => a.ScrapingHistoryId);

        runs.Should().BeEquivalentTo(new Dictionary<string, int> { ["om-1"] = 1, ["iw-1"] = 2, ["is-1"] = 3 });
    }

    [Test]
    public async Task AFlatOlderThanEveryRun_IsLeftOut()
    {
        (await _dbContext.FlatsArchive.AnyAsync(a => a.ListingId == "om-old")).Should().BeFalse();
    }

    [Test]
    public async Task ArchivedRows_KeepTheFlatsValuesAndFirstSeenTime()
    {
        var archived = await _dbContext.FlatsArchive.SingleAsync(a => a.ListingId == "om-1");

        archived.Source.Should().Be("OhneMakler");
        archived.Url.Should().Be("https://example.org/om-1");
        archived.PriceEur.Should().Be(300000);
        archived.Details!.Heating.Should().Be("Gas");
        archived.ScrapedAtUtc.Should().BeCloseTo(new DateTime(2026, 11, 3, 8, 5, 0, 500, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        _dbContext.Entry(archived).Property<DateTime>("CreatedAtUtc").CurrentValue
            .Should().BeCloseTo(new DateTime(2026, 10, 29, 12, 54, 24, 565, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task Previews_AreCopiedIntoTheArchive()
    {
        var preview = await _dbContext.FlatPreviewsArchive.SingleAsync();

        preview.ListingId.Should().Be("om-1");
        preview.ImageUrl.Should().Be("https://example.org/om-1.webp");
        preview.ContentType.Should().Be("image/webp");
        preview.Data.Should().Equal(1, 2, 3);
    }

    [Test]
    public async Task CurrentFlatsAndPreviews_AreKept()
    {
        (await _dbContext.FlatsSnapshots.CountAsync()).Should().Be(4);
        (await _dbContext.FlatPreviews.CountAsync()).Should().Be(1);
    }
}
