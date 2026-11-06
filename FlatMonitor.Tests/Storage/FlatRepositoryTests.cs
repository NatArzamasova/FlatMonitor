using NUnit.Framework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using FlatMonitor.Models;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class FlatRepositoryTests
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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

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
        var repo = new FlatRepository(repoContext);

        await using (var dbContext = new FlatsDbContext(options))
        {
            var older = new PublishedFlat
            {
                Source = "source", ListingId = "older", ScrapedAtUtc = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                District = "Innenstadt", RoomsCount = 2, PriceEur = 900, PricePerMeter = 30, Url = "old-url"
            };
            var newer = new PublishedFlat
            {
                Source = "source", ListingId = "newer", ScrapedAtUtc = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
                District = "Zentrum", RoomsCount = 3, PriceEur = 1200, OriginalPriceEur = 1400,
                PricePerMeter = 40, LivingAreaSqm = 72, Url = "new-url", CommissionPercent = 3.57,
                Details = new FlatDetails { Availability = "sofort", EnergyEfficiencyClass = "A_PLUS", ServiceChargeEur = 310 }
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
        result[0].Availability.Should().Be("sofort");
        result[1].Availability.Should().BeNull("the older flat has no details");
        result[0].EnergyEfficiencyClass.Should().Be("A_PLUS");
        result[1].EnergyEfficiencyClass.Should().BeNull();
        result[0].ServiceChargeEur.Should().Be(310);
        result[1].ServiceChargeEur.Should().BeNull();
        result[0].CommissionPercent.Should().Be(3.57);
        result[1].CommissionPercent.Should().BeNull();
    }

    private DbContextOptions<FlatsDbContext> CreateSqliteOptions()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        return new DbContextOptionsBuilder<FlatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    [Test]
    public async Task GetCurrentOffersAsync_OnSqlite_ReadsTheOfferDetailsFromTheDetailsColumns()
    {
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);

        await using (var dbContext = new FlatsDbContext(options))
        {
            var withDetails = new PublishedFlat
            {
                Source = "source", ListingId = "with-details", ScrapedAtUtc = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
                District = "Zentrum", Details = new FlatDetails { Availability = "01.03.2027", EnergyEfficiencyClass = "C", ServiceChargeEur = 245 }
            };
            var withoutDetails = new PublishedFlat
            {
                Source = "source", ListingId = "without-details", ScrapedAtUtc = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                District = "Innenstadt"
            };
            dbContext.FlatsSnapshots.AddRange(withDetails, withoutDetails);
            var createdAt = DateTime.UtcNow;
            foreach (var snapshot in new[] { withDetails, withoutDetails })
            {
                dbContext.Entry(snapshot).Property<DateTime>("CreatedAtUtc").CurrentValue = createdAt;
                dbContext.Entry(snapshot).Property<DateTime>("UpdatedAtUtc").CurrentValue = createdAt;
            }
            await dbContext.SaveChangesAsync();
        }

        var result = await repo.GetCurrentOffersAsync();

        result.Select(offer => offer.Availability).Should().Equal("01.03.2027", null);
        result.Select(offer => offer.EnergyEfficiencyClass).Should().Equal("C", null);
        result.Select(offer => offer.ServiceChargeEur).Should().Equal(245, null);
    }

    [Test]
    public async Task GetCurrentOffersAsync_WithCanceledToken_Throws()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
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
        var repo = new FlatRepository(repoContext);
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
        await new FlatRepository(repoContext).AddToRunAsync([CreateFlat("1") with { ScrapedAtUtc = scrapedAt }]);

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
    public async Task AddToRunAsync_AfterSaving_NoLongerTracksFlatRows()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
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
    public async Task AddToRunAsync_WhenSavingFails_NoLongerTracksTheBatch()
    {
        // Arrange: two flats the unique index (Source, Url, Address) rejects together
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        var clash = CreateFlat("2") with { Url = CreateFlat("1").Url, Address = CreateFlat("1").Address };

        // Act
        var act = () => repo.AddToRunAsync([CreateFlat("1"), clash]);

        // Assert: the failed rows are not saved again by the next SaveChanges on the shared context
        await act.Should().ThrowAsync<DbUpdateException>();
        repoContext.ChangeTracker.Entries().Should().BeEmpty();
        repoContext.ScrapingHistories.Add(new ScrapingHistory { StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Running });
        await repoContext.Invoking(c => c.SaveChangesAsync()).Should().NotThrowAsync();
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
        await new FlatRepository(repoContext).AddToRunAsync([CreateFlat("1")]);

        // Assert
        repoContext.Entry(history).State.Should().Be(EntityState.Unchanged);
    }

    [Test]
    public async Task AddToRunAsync_LeavesRowsOutsideTheBatchUnchanged()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
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
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24"));

        // Act: this run finds OhneMakler 2 (with a new price) and 3
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("2") with { PriceEur = 250000 }, CreateFlat("3")]);
        var published = await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

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
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"));
        var firstSeen = await AuditDatesAsync(options, "1");

        // Act
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("1") with { Title = "Updated" }, CreateFlat("2")]);
        await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

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
        var repo = new FlatRepository(repoContext);
        var flat = CreateFlat("1") with
        {
            PostalCode = "76131", District = "Oststadt", PriceEur = 300000, OriginalPriceEur = 320000, RoomsCount = 3,
            LivingAreaSqm = 75, CommissionPercent = 3.57, Details = new FlatDetails { Heating = "Gas", BuildYear = 1976 },
        };

        // Act
        await repo.AddToRunAsync([flat]);
        await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.SingleAsync();
        stored.Should().BeEquivalentTo(PublishedFlat.From(flat), o => o
            .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromMilliseconds(1))).WhenTypeIs<DateTime>());
    }

    [Test]
    public async Task PublishSourceAsync_AfterSaving_NoLongerTracksFlatRows()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"));

        // Act
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("3")]);
        await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

        // Assert
        repoContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Test]
    public async Task BeginRunAsync_EmptiesTheStagingTableButNotFlatsSnapshots()
    {
        // Arrange: an interrupted run left a staged flat
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("published"));
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
        var repo = new FlatRepository(repoContext);
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("1"), CreateFlat("2", source: "ImmobilienScout24")]);
        await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

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

    /// <summary>
    /// Publishes the flats as a finished run would: stage, publish each source, empty the staging table. Returns the
    /// run's id.
    /// </summary>
    private static async Task<int> PublishAsync(FlatsDbContext dbContext, FlatRepository repo, params Flat[] flats)
    {
        var runId = await TestRuns.AddAsync(dbContext);
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([.. flats]);
        foreach (var source in flats.Select(f => f.Source).Distinct())
            await repo.PublishSourceAsync(source, runId);
        await repo.DiscardRunAsync();
        return runId;
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

    // ==================== ARCHIVE ====================

    [Test]
    public async Task PublishSourceAsync_ArchivesEveryPublishedFlatWithTheRun()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        var flat = CreateFlat("1") with
        {
            PostalCode = "76131", District = "Oststadt", PriceEur = 300000, OriginalPriceEur = 320000, RoomsCount = 3,
            LivingAreaSqm = 75, CommissionPercent = 3.57, Details = new FlatDetails { Heating = "Gas", BuildYear = 1976 },
        };
        var runId = await TestRuns.AddAsync(repoContext);

        // Act
        await repo.AddToRunAsync([flat, CreateFlat("2")]);
        await repo.PublishSourceAsync("OhneMakler", runId);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var archived = await dbContext.FlatsArchive.OrderBy(a => a.ListingId).ToListAsync();
        archived.Select(a => (a.ListingId, a.ScrapingHistoryId)).Should().Equal(("1", runId), ("2", runId));
        archived[0].Should().BeEquivalentTo(ArchivedFlat.From(PublishedFlat.From(flat), runId), o => o
            .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromMilliseconds(1))).WhenTypeIs<DateTime>());
    }

    [Test]
    public async Task PublishSourceAsync_ArchivesOnlyThePublishedSourceAndNotTheRemovedFlats()
    {
        // Arrange: the previous run published OhneMakler 1; this run stages OhneMakler 2 and ImmobilienScout24 1
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        var firstRun = await PublishAsync(repoContext, repo, CreateFlat("1"));
        var runId = await TestRuns.AddAsync(repoContext);
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24")]);

        // Act: ImmobilienScout24 fails and is not published
        await repo.PublishSourceAsync("OhneMakler", runId);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsArchive.Select(a => new { a.Source, a.ListingId, a.ScrapingHistoryId }).ToListAsync())
            .Should().BeEquivalentTo([
                new { Source = "OhneMakler", ListingId = "1", ScrapingHistoryId = firstRun },
                new { Source = "OhneMakler", ListingId = "2", ScrapingHistoryId = runId },
            ]);
    }

    [Test]
    public async Task PublishSourceAsync_InTheNextRun_AddsRowsAndLeavesTheEarlierOnesAsTheyWere()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        var firstRun = await PublishAsync(repoContext, repo, CreateFlat("1") with { PriceEur = 300000 });

        // Act: the price dropped
        var secondRun = await PublishAsync(repoContext, repo, CreateFlat("1") with { PriceEur = 280000 });

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsArchive.OrderBy(a => a.ScrapingHistoryId).Select(a => new { a.ScrapingHistoryId, a.PriceEur }).ToListAsync())
            .Should().Equal(new { ScrapingHistoryId = firstRun, PriceEur = (int?)300000 }, new { ScrapingHistoryId = secondRun, PriceEur = (int?)280000 });
    }

    [Test]
    public async Task PublishSourceAsync_ArchivesTheFirstSeenTimeOfAFlatStillOffered()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"));
        var firstSeen = await AuditDatesAsync(options, "1");

        // Act
        var secondRun = await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"));

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var archived = await dbContext.FlatsArchive.Where(a => a.ScrapingHistoryId == secondRun).ToDictionaryAsync(a => a.ListingId);
        var still = dbContext.Entry(archived["1"]);
        still.Property<DateTime>("CreatedAtUtc").CurrentValue.Should().Be(firstSeen.CreatedAtUtc);
        still.Property<DateTime>("UpdatedAtUtc").CurrentValue.Should().BeAfter(firstSeen.UpdatedAtUtc);
        var added = dbContext.Entry(archived["2"]);
        added.Property<DateTime>("CreatedAtUtc").CurrentValue.Should().Be((await AuditDatesAsync(options, "2")).CreatedAtUtc);
    }

    [Test]
    public async Task PublishSourceAsync_WhenTheArchiveCannotBeWritten_PublishesNothing()
    {
        // Arrange: an unknown run fails the archive's foreign key
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"));
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("2")]);

        // Act
        var publish = () => repo.PublishSourceAsync("OhneMakler", scrapingHistoryId: 999);

        // Assert: one transaction, so FlatsSnapshots keeps the previous run; and nothing is left for the next save
        await publish.Should().ThrowAsync<DbUpdateException>();
        repoContext.ChangeTracker.Entries().Should().BeEmpty();
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshots.Select(s => s.ListingId).ToListAsync()).Should().Equal("1");
        (await dbContext.FlatsArchive.Select(a => a.ListingId).ToListAsync()).Should().Equal("1");
    }

    // ==================== ARCHIVED OFFERS ====================

    private static DateTime Scraped(int day) => new(2026, 11, day, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetArchivedOffersAsync_ListsTheRemovedFlatsWithTheirLatestValues_MostRecentlySeenFirst()
    {
        // Arrange: 1 is seen in two runs (its price drops), 2 in one; both are gone in the third run
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo,
            CreateFlat("1") with { PriceEur = 300000, ScrapedAtUtc = Scraped(1) },
            CreateFlat("2") with { ScrapedAtUtc = Scraped(1) });
        var firstSeen = await AuditDatesAsync(options, "1");
        await PublishAsync(repoContext, repo,
            CreateFlat("1") with
            {
                District = "Oststadt", RoomsCount = 3, PriceEur = 280000, OriginalPriceEur = 300000,
                LivingAreaSqm = 75, CommissionPercent = 3.57, ScrapedAtUtc = Scraped(2),
                Details = new FlatDetails { Availability = "sofort", EnergyEfficiencyClass = "B", ServiceChargeEur = 250 },
            },
            CreateFlat("3") with { ScrapedAtUtc = Scraped(2) });
        await PublishAsync(repoContext, repo, CreateFlat("3") with { ScrapedAtUtc = Scraped(3) });

        // Act
        var result = await repo.GetArchivedOffersAsync();

        // Assert
        result.Select(o => o.ListingId).Should().Equal("1", "2");
        var offer = result[0];
        offer.Should().BeEquivalentTo(new
        {
            Source = "OhneMakler", ListingId = "1", Url = "/immobilie/1/", District = "Oststadt", RoomsCount = 3.0,
            PriceEur = 280000, OriginalPriceEur = 300000, PricePerMeter = 3733, LivingAreaSqm = 75.0, CommissionPercent = 3.57,
            Availability = "sofort", EnergyEfficiencyClass = "B", ServiceChargeEur = 250, HasPreview = false,
        });
        offer.LastSeenUtc.Should().Be(Scraped(2));
        offer.FirstSeenUtc.Should().BeCloseTo(firstSeen.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        offer.FirstPriceEur.Should().Be(300000, "the price of its earliest run");
        result[1].LastSeenUtc.Should().Be(Scraped(1));
    }

    [Test]
    public async Task GetArchivedOffersAsync_FirstPrice_IsThePriceOfTheEarliestRun()
    {
        // Arrange: 1 is seen with three prices, 2 once, 3 without a price at first
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1") with { PriceEur = 320000 }, CreateFlat("3"));
        await PublishAsync(repoContext, repo, CreateFlat("1") with { PriceEur = 300000 }, CreateFlat("3") with { PriceEur = 200000 });
        await PublishAsync(repoContext, repo, CreateFlat("1") with { PriceEur = 310000 }, CreateFlat("2") with { PriceEur = 250000 });
        await PublishAsync(repoContext, repo, CreateFlat("4"));

        // Act
        var result = await repo.GetArchivedOffersAsync();

        // Assert
        result.ToDictionary(o => o.ListingId!, o => (o.FirstPriceEur, o.PriceEur)).Should().BeEquivalentTo(new Dictionary<string, (int?, int?)>
        {
            ["1"] = (320000, 310000),
            ["2"] = (250000, 250000),
            ["3"] = (null, 200000),
        });
    }

    [Test]
    public async Task GetArchivedOffersAsync_LeavesOutTheFlatsStillOrAgainOffered()
    {
        // Arrange: 1 is missed by the second run and found again by the third; 2 is offered throughout
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"));
        await PublishAsync(repoContext, repo, CreateFlat("2"));
        (await repo.GetArchivedOffersAsync()).Select(o => o.ListingId).Should().Equal("1");

        // Act
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"));

        // Assert
        (await repo.GetArchivedOffersAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task GetArchivedOffersAsync_KeepsTheFlatsOfAnotherSource_WhileThatSourceKeepsThem()
    {
        // Arrange: ImmobilienScout24 is not scraped in the second run (or fails), so it keeps its flat
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("1", source: "ImmobilienScout24"));

        // Act
        await PublishAsync(repoContext, repo, CreateFlat("2"));

        // Assert: the same listing id at another source is another flat
        (await repo.GetArchivedOffersAsync()).Select(o => o.Source + "/" + o.ListingId).Should().Equal("OhneMakler/1");
    }

    [Test]
    public async Task GetArchivedOffersAsync_FirstSeenIsTheEarliest_ForAFlatRemovedTwice()
    {
        // Arrange: 1 is removed, comes back as a new FlatsSnapshots row (a new CreatedAtUtc), and is removed again
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1") with { ScrapedAtUtc = Scraped(1) }, CreateFlat("2"));
        var firstSeen = await AuditDatesAsync(options, "1");
        await PublishAsync(repoContext, repo, CreateFlat("2"));
        await PublishAsync(repoContext, repo, CreateFlat("1") with { ScrapedAtUtc = Scraped(3) }, CreateFlat("2"));
        (await AuditDatesAsync(options, "1")).CreatedAtUtc.Should().BeAfter(firstSeen.CreatedAtUtc);
        await PublishAsync(repoContext, repo, CreateFlat("2"));

        // Act
        var offer = (await repo.GetArchivedOffersAsync()).Should().ContainSingle().Subject;

        // Assert
        offer.FirstSeenUtc.Should().BeCloseTo(firstSeen.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        offer.LastSeenUtc.Should().Be(Scraped(3));
    }

    [Test]
    public async Task GetArchivedOffersAsync_ReturnsUtcDates()
    {
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"));
        await PublishAsync(repoContext, repo, CreateFlat("2"));

        var offer = (await repo.GetArchivedOffersAsync()).Single();

        offer.FirstSeenUtc.Kind.Should().Be(DateTimeKind.Utc);
        offer.LastSeenUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public async Task GetArchivedOffersAsync_TellsWhichOffersHaveAnArchivedPhoto()
    {
        // Arrange: publishing the second run deletes the current previews of 1 and 2; their archived photos stay
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"));
        await new FlatPreviewRepository(repoContext).SaveAsync([Preview("OhneMakler", "1")]);

        // Act
        await PublishAsync(repoContext, repo, CreateFlat("3"));
        var result = await repo.GetArchivedOffersAsync();

        // Assert
        result.ToDictionary(o => o.ListingId!, o => o.HasPreview)
            .Should().Equal(new Dictionary<string, bool> { ["1"] = true, ["2"] = false });
    }

    [Test]
    public async Task GetArchivedOffersAsync_WithAnEmptyArchive_ReturnsNothing()
    {
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);

        (await new FlatRepository(repoContext).GetArchivedOffersAsync()).Should().BeEmpty();
    }

    // ==================== PREVIEWS ====================

    [Test]
    public async Task PublishSourceAsync_DeletesThePreviewsOfFlatsTheSourceNoLongerHas()
    {
        // Arrange: OhneMakler 1 and 2 published with previews; a failed run left a preview of 9, never published
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24"));
        await new FlatPreviewRepository(repoContext).SaveAsync(
            [Preview("OhneMakler", "1"), Preview("OhneMakler", "2"), Preview("OhneMakler", "9"), Preview("ImmobilienScout24", "1")]);

        // Act: this run finds OhneMakler 2 and 3
        await repo.BeginRunAsync();
        await repo.AddToRunAsync([CreateFlat("2"), CreateFlat("3")]);
        await repo.PublishSourceAsync("OhneMakler", await TestRuns.AddAsync(repoContext));

        // Assert: the other source's previews stay
        await using var dbContext = new FlatsDbContext(options);
        var previews = await dbContext.FlatPreviews.Select(p => new { p.Source, p.ListingId }).ToListAsync();
        previews.Select(p => (p.Source, p.ListingId)).Should().BeEquivalentTo([("OhneMakler", "2"), ("ImmobilienScout24", "1")]);
        repoContext.ChangeTracker.Entries<FlatPreview>().Should().BeEmpty();
        // The archived photos stay, also of the removed flats
        (await dbContext.FlatPreviewsArchive.Select(p => p.Source + "/" + p.ListingId).ToListAsync())
            .Should().BeEquivalentTo("OhneMakler/1", "OhneMakler/2", "OhneMakler/9", "ImmobilienScout24/1");
    }

    [Test]
    public async Task GetCurrentOffersAsync_OnSqlite_TellsWhichOffersHaveAPreview()
    {
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatRepository(repoContext);
        await PublishAsync(repoContext, repo, CreateFlat("1"), CreateFlat("2"), CreateFlat("1", source: "ImmobilienScout24"));
        await new FlatPreviewRepository(repoContext).SaveAsync([Preview("OhneMakler", "1")]);

        var result = await repo.GetCurrentOffersAsync();

        result.ToDictionary(offer => (offer.Source, offer.ListingId), offer => offer.HasPreview).Should().Equal(
            new Dictionary<(string?, string?), bool>
            {
                [("OhneMakler", "1")] = true,
                [("OhneMakler", "2")] = false,
                [("ImmobilienScout24", "1")] = false,
            });
    }

    private static FlatPreview Preview(string source, string listingId) => new()
    {
        Source = source,
        ListingId = listingId,
        ImageUrl = $"https://pictures.example/{source}/{listingId}.jpg",
        ContentType = "image/jpeg",
        Data = [1, 2, 3],
        DownloadedAtUtc = DateTime.UtcNow,
    };
}
