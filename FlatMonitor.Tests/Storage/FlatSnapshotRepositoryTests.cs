using NUnit.Framework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using FlatMonitor.Models;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class FlatSnapshotRepositoryTests
{
    private DbContextOptions<FlatsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private static async Task<FlatsDbContext> CreateContextAsync(DbContextOptions<FlatsDbContext> options)
    {
        var context = new FlatsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Test]
    public async Task AddToRunAsync_InsertsNewListing()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test Listing",
                Address = "123 Main St",
                PostalCode = "76131",
                District = "Innenstadt",
                PriceEur = 500000,
                RoomsCount = 3,
                LivingAreaSqm = 85,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = new FlatDetails { Heating = "Gas" }
            }
        };

        // Act
        await repo.AddToRunAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshotsStaging.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().NotBeNull();
        stored.Title.Should().Be("Test Listing");
        stored.Address.Should().Be("123 Main St");
        stored.PriceEur.Should().Be(500000);
        stored.Source.Should().Be("OhneMakler");
    }

    [Test]
    public async Task AddToRunAsync_WithDetails_PersistsDetailFields()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                Address = "Test Address",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = new FlatDetails
                {
                    PropertyType = "Wohnung",
                    BuildYear = 2010,
                    Floor = "2",
                    Condition = "Renoviert",
                    Heating = "Gas",
                    ServiceChargeEur = 150,
                    ListingStatus = "Privatangebot",
                }
            }
        };

        // Act
        await repo.AddToRunAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshotsStaging.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().NotBeNull();
        stored.Details!.PropertyType.Should().Be("Wohnung");
        stored.Details.BuildYear.Should().Be(2010);
        stored.Details.Floor.Should().Be("2");
        stored.Details.Heating.Should().Be("Gas");
        stored.Details.ServiceChargeEur.Should().Be(150);
    }

    [Test]
    public async Task AddToRunAsync_WithNullDetails_StoresNullsForDetailFields()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                Address = "Test Address",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = null,
            }
        };

        // Act
        await repo.AddToRunAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshotsStaging.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().BeNull();
    }

    [Test]
    public async Task AddToRunAsync_CalledTwiceWithSameListingId_UpdatesExistingRow()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listing1 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original Title",
                Address = "Original Address",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = new FlatDetails { Heating = "Gas" }
            }
        };

        await repo.AddToRunAsync(listing1);

        var listing2 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Updated Title",
                Address = "Updated Address",
                PriceEur = 550000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = new FlatDetails { Heating = "Gas" }
            }
        };

        // Act
        await repo.AddToRunAsync(listing2);

        // Assert - row count should stay 1
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshotsStaging.CountAsync();
        count.Should().Be(1);

        var stored = await dbContext.FlatsSnapshotsStaging.FirstAsync();
        stored.Details.Should().NotBeNull();
        stored.Title.Should().Be("Updated Title");
        stored.PriceEur.Should().Be(550000);
    }

    [Test]
    public async Task AddToRunAsync_WithMultipleListings_InsertsAllRows()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Listing 1",
                Address = "Address 1",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            },
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "Listing 2",
                Address = "Address 2",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            },
            new()
            {
                ListingId = "3",
                Url = "/immobilie/3/",
                Title = "Listing 3",
                Address = "Address 3",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };

        // Act
        await repo.AddToRunAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshotsStaging.CountAsync();
        count.Should().Be(3);
    }

    [Test]
    public async Task AddToRunAsync_WithMultipleSources_PersistsBothAsSeperateRows()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "123",
                Url = "/immobilie/123/",
                Title = "From OhneMakler",
                Address = "Address A",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
                Details = new FlatDetails { Heating = "Gas" }
            },
            new()
            {
                ListingId = "123",
                Url = "/property/123",
                Title = "From ImmobilienScout24",
                Address = "Address B",
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "ImmobilienScout24",
                Details = new FlatDetails { Heating = "Gas" }
            }
        };

        // Act
        await repo.AddToRunAsync(listings);

        // Assert - both rows with same ListingId but different Source should exist
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshotsStaging.CountAsync();
        count.Should().Be(2);

        var ohmRow = await dbContext.FlatsSnapshotsStaging.FirstOrDefaultAsync(l => l.Source == "OhneMakler" && l.ListingId == "123");
        ohmRow.Should().NotBeNull();
        ohmRow!.Details.Should().NotBeNull();
        ohmRow.Title.Should().Be("From OhneMakler");

        var scoutRow = await dbContext.FlatsSnapshotsStaging.FirstOrDefaultAsync(l => l.Source == "ImmobilienScout24" && l.ListingId == "123");
        scoutRow.Should().NotBeNull();
        scoutRow!.Details.Should().NotBeNull();
        scoutRow.Title.Should().Be("From ImmobilienScout24");
    }

    [Test]
    public async Task GetCurrentOffersAsync_OrdersByMostRecentScrapeAndMapsOfferFields()
    {
        var options = CreateInMemoryOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        await using (var dbContext = new FlatsDbContext(options))
        {
            var older = new FlatSnapshot
            {
                Source = "source", ListingId = "older", ScrapedAtUtc = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                District = "Innenstadt", RoomsCount = 2, PriceEur = 900, PricePerMeter = 30, Url = "old-url"
            };
            var newer = new FlatSnapshot
            {
                Source = "source", ListingId = "newer", ScrapedAtUtc = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
                District = "Zentrum", RoomsCount = 3, PriceEur = 1200, OriginalPriceEur = 1400,
                PricePerMeter = 40, LivingAreaSqm = 72, Url = "new-url"
            };
            dbContext.FlatsSnapshots.AddRange(older, newer);
            var createdAt = DateTime.UtcNow;
            foreach (var snapshot in new[] { older, newer })
            {
                dbContext.Entry(snapshot).Property<DateTime>("CreatedAtUtc").CurrentValue = createdAt;
                dbContext.Entry(snapshot).Property<DateTime>("UpdatedAtUtc").CurrentValue = createdAt;
            }
            await dbContext.SaveChangesAsync();
        }

        var result = await repo.GetCurrentOffersAsync();

        result.Should().HaveCount(2);
        result[0].District.Should().Be("Zentrum");
        result[0].RoomsCount.Should().Be(3);
        result[0].PriceEur.Should().Be(1200);
        result[0].OriginalPriceEur.Should().Be(1400);
        result[0].PricePerMeter.Should().Be(40);
        result[0].LivingAreaSqm.Should().Be(72);
        result[0].Url.Should().Be("new-url");
        result[0].Source.Should().Be("source");
    }

    private DbContextOptions<FlatsDbContext> CreateSqliteOptions()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        return new DbContextOptionsBuilder<FlatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    [Test]
    public async Task GetCurrentOffersAsync_WithCanceledToken_Throws()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => repo.GetCurrentOffersAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task AddToRunAsync_UpdatingAFlat_StoresEveryField()
    {
        // Arrange: the old upsert left Title and CommissionPercent unchanged on update
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.AddToRunAsync([CreateFlat("1") with { CommissionPercent = 3.57, PriceEur = 300000, LivingAreaSqm = 75 }]);

        // Act
        await repo.AddToRunAsync([CreateFlat("1") with { Title = "New title", CommissionPercent = 0, PriceEur = 150000, LivingAreaSqm = 75 }]);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshotsStaging.SingleAsync();
        stored.Title.Should().Be("New title");
        stored.CommissionPercent.Should().Be(0);
        stored.PricePerMeter.Should().Be(2000);
    }

    [Test]
    public async Task AddToRunAsync_DatesReadBackAsUtc()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var scrapedAt = new DateTime(2026, 10, 29, 8, 7, 48, DateTimeKind.Utc);

        // Act
        await new FlatSnapshotRepository(repoContext).AddToRunAsync([CreateFlat("1") with { ScrapedAtUtc = scrapedAt }]);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshotsStaging.SingleAsync();
        stored.ScrapedAtUtc.Should().Be(scrapedAt);
        stored.ScrapedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        var createdAt = dbContext.Entry(stored).Property<DateTime>("CreatedAtUtc").CurrentValue;
        createdAt.Kind.Should().Be(DateTimeKind.Utc);
        createdAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task AddToRunAsync_AfterSaving_NoLongerTracksSnapshots()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("2")]);

        // Act
        await repo.AddToRunAsync([CreateFlat("1") with { Title = "Updated" }, CreateFlat("3")]);

        // Assert
        repoContext.ChangeTracker.Entries().Should().BeEmpty();
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshotsStaging.CountAsync()).Should().Be(3);
        (await dbContext.FlatsSnapshotsStaging.SingleAsync(s => s.ListingId == "1")).Title.Should().Be("Updated");
    }

    [Test]
    public async Task AddToRunAsync_KeepsOtherEntitiesOfTheSharedContextTracked()
    {
        // Arrange: the background service tracks its history row on the same scoped context
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var history = new ScrapingHistory { StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Running };
        repoContext.ScrapingHistories.Add(history);
        await repoContext.SaveChangesAsync();

        // Act
        await new FlatSnapshotRepository(repoContext).AddToRunAsync([CreateFlat("1")]);

        // Assert
        repoContext.Entry(history).State.Should().Be(EntityState.Unchanged);
    }

    [Test]
    public async Task AddToRunAsync_LeavesRowsOutsideTheBatchUnchanged()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24")]);

        // Act
        await repo.AddToRunAsync([
            CreateFlat("1") with { Title = "OM updated" },
            CreateFlat("1", source: "ImmobilienScout24") with { Title = "IS24 updated" }]);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var rows = await dbContext.FlatsSnapshotsStaging.ToDictionaryAsync(s => (s.Source, s.ListingId), s => s.Title);
        rows.Should().HaveCount(3);
        rows[("OhneMakler", "1")].Should().Be("OM updated");
        rows[("OhneMakler", "2")].Should().Be("Listing 2");
        rows[("ImmobilienScout24", "1")].Should().Be("IS24 updated");
    }

    // ==================== RUN: BEGIN / PUBLISH / DISCARD ====================

    [Test]
    public async Task PublishSourceAsync_ReplacesTheSourcesRowsAndLeavesOtherSourcesUntouched()
    {
        // Arrange: the previous run published OhneMakler 1, 2 and ImmobilienScout24 1
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await PublishAsync(repo, CreateFlat("1"), CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24"));

        // Act: this run finds OhneMakler 2 (with a new price) and 3
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("2") with { PriceEur = 250000 }, CreateFlat("3")]);
        var published = await repo.PublishSourceAsync("OhneMakler");

        // Assert
        published.Should().Be(2);
        await using var dbContext = new FlatsDbContext(options);
        var rows = await dbContext.FlatsSnapshots.ToDictionaryAsync(s => (s.Source, s.ListingId), s => s.PriceEur);
        rows.Keys.Should().BeEquivalentTo([("OhneMakler", "2"), ("OhneMakler", "3"), ("ImmobilienScout24", "1")]);
        rows[("OhneMakler", "2")].Should().Be(250000);
    }

    [Test]
    public async Task PublishSourceAsync_KeepsCreatedAtOfAFlatStillOfferedAndUpdatesTheRest()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await PublishAsync(repo, CreateFlat("1"));
        var firstSeen = await AuditDatesAsync(options, "1");

        // Act
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("1") with { Title = "Updated" }, CreateFlat("2")]);
        await repo.PublishSourceAsync("OhneMakler");

        // Assert
        var still = await AuditDatesAsync(options, "1");
        still.CreatedAtUtc.Should().Be(firstSeen.CreatedAtUtc, "the flat was first seen in the previous run");
        still.UpdatedAtUtc.Should().BeAfter(firstSeen.UpdatedAtUtc);
        var added = await AuditDatesAsync(options, "2");
        added.CreatedAtUtc.Should().Be(added.UpdatedAtUtc).And.BeAfter(firstSeen.CreatedAtUtc);
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshots.SingleAsync(s => s.ListingId == "1")).Title.Should().Be("Updated");
    }

    [Test]
    public async Task PublishSourceAsync_CopiesEveryStoredField()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        var flat = CreateFlat("1") with
        {
            PostalCode = "76131", District = "Oststadt", PriceEur = 300000, OriginalPriceEur = 320000, RoomsCount = 3,
            LivingAreaSqm = 75, CommissionPercent = 3.57, Details = new FlatDetails { Heating = "Gas", BuildYear = 1976 },
        };

        // Act
        await repo.AddToRunAsync([flat]);
        await repo.PublishSourceAsync("OhneMakler");

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.SingleAsync();
        stored.Should().BeEquivalentTo(FlatSnapshot.From(flat), o => o
            .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromMilliseconds(1))).WhenTypeIs<DateTime>());
    }

    [Test]
    public async Task PublishSourceAsync_AfterSaving_NoLongerTracksFlatRows()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await PublishAsync(repo, CreateFlat("1"), CreateFlat("2"));

        // Act
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("3")]);
        await repo.PublishSourceAsync("OhneMakler");

        // Assert
        repoContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Test]
    public async Task BeginRunAsync_EmptiesTheStagingTableButNotFlatsSnapshots()
    {
        // Arrange: an interrupted run left a staged flat
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await PublishAsync(repo, CreateFlat("published"));
        await repo.AddToRunAsync([CreateFlat("leftover")]);

        // Act
        await repo.BeginRunAsync();

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshotsStaging.AnyAsync()).Should().BeFalse();
        (await dbContext.FlatsSnapshots.SingleAsync()).ListingId.Should().Be("published");
    }

    [Test]
    public async Task DiscardRunAsync_EmptiesTheStagingTableAndKeepsWhatWasPublished()
    {
        // Arrange: OhneMakler was published, ImmobilienScout24 failed after staging a flat
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("2", source: "ImmobilienScout24")]);
        await repo.PublishSourceAsync("OhneMakler");

        // Act
        await repo.DiscardRunAsync();

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshotsStaging.AnyAsync()).Should().BeFalse();
        (await dbContext.FlatsSnapshots.Select(s => s.Source + "/" + s.ListingId).ToListAsync()).Should().Equal("OhneMakler/1");
    }

    [Test]
    public async Task StagingTable_HasTheSameColumnsAsFlatsSnapshots()
    {
        // Publishing copies row by row; a column missing in one of the tables would lose data
        var options = CreateSqliteOptions();
        await using var dbContext = await CreateContextAsync(options);

        var live = await ColumnsAsync(dbContext, "FlatsSnapshots");
        var staging = await ColumnsAsync(dbContext, "FlatsSnapshots_Staging");

        staging.Should().NotBeEmpty().And.Equal(live);
    }

    /// <summary>Publishes the flats as a finished run would: stage, publish each source, empty the staging table.</summary>
    private static async Task PublishAsync(FlatSnapshotRepository repo, params Flat[] flats)
    {
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([.. flats]);
        foreach (var source in flats.Select(f => f.Source).Distinct())
            await repo.PublishSourceAsync(source);
        await repo.DiscardRunAsync();
    }

    private static async Task<(DateTime CreatedAtUtc, DateTime UpdatedAtUtc)> AuditDatesAsync(
        DbContextOptions<FlatsDbContext> options, string listingId)
    {
        await using var dbContext = new FlatsDbContext(options);
        var row = await dbContext.FlatsSnapshots.SingleAsync(s => s.ListingId == listingId);
        var entry = dbContext.Entry(row);
        return (entry.Property<DateTime>("CreatedAtUtc").CurrentValue, entry.Property<DateTime>("UpdatedAtUtc").CurrentValue);
    }

    private static Task<List<string>> ColumnsAsync(FlatsDbContext dbContext, string table) =>
        dbContext.Database
            .SqlQueryRaw<string>("SELECT name || ' ' || type || ' ' || \"notnull\" AS Value FROM pragma_table_info({0}) ORDER BY name", table)
            .ToListAsync();

    private static Flat CreateFlat(string listingId, string source = "OhneMakler") => new()
    {
        ListingId = listingId,
        Url = $"/immobilie/{listingId}/",
        Title = $"Listing {listingId}",
        Address = $"Address {listingId}",
        ScrapedAtUtc = DateTime.UtcNow,
        Source = source,
    };
}
