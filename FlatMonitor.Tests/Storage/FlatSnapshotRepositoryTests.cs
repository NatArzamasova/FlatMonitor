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
    public async Task UpsertListingsAsync_InsertsNewListing()
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
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().NotBeNull();
        stored.Title.Should().Be("Test Listing");
        stored.Address.Should().Be("123 Main St");
        stored.PriceEur.Should().Be(500000);
        stored.Source.Should().Be("OhneMakler");
    }

    [Test]
    public async Task UpsertListingsAsync_WithDetails_PersistsDetailFields()
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
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().NotBeNull();
        stored.Details!.PropertyType.Should().Be("Wohnung");
        stored.Details.BuildYear.Should().Be(2010);
        stored.Details.Floor.Should().Be("2");
        stored.Details.Heating.Should().Be("Gas");
        stored.Details.ServiceChargeEur.Should().Be(150);
    }

    [Test]
    public async Task UpsertListingsAsync_WithNullDetails_StoresNullsForDetailFields()
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
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().BeNull();
    }

    [Test]
    public async Task UpsertListingsAsync_CalledTwiceWithSameListingId_UpdatesExistingRow()
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

        await repo.UpsertListingsAsync(listing1);

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
        await repo.UpsertListingsAsync(listing2);

        // Assert - row count should stay 1
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(1);

        var stored = await dbContext.FlatsSnapshots.FirstAsync();
        stored.Details.Should().NotBeNull();
        stored.Title.Should().Be("Updated Title");
        stored.PriceEur.Should().Be(550000);
    }

    [Test]
    public async Task UpsertListingsAsync_WithMultipleListings_InsertsAllRows()
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
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(3);
    }

    [Test]
    public async Task UpsertListingsAsync_WithMultipleSources_PersistsBothAsSeperateRows()
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
        await repo.UpsertListingsAsync(listings);

        // Assert - both rows with same ListingId but different Source should exist
        await using var dbContext = new FlatsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(2);

        var ohmRow = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.Source == "OhneMakler" && l.ListingId == "123");
        ohmRow.Should().NotBeNull();
        ohmRow!.Details.Should().NotBeNull();
        ohmRow.Title.Should().Be("From OhneMakler");

        var scoutRow = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.Source == "ImmobilienScout24" && l.ListingId == "123");
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

    // ==================== BACKUP/RESTORE TESTS ====================
    // Note: Backup/restore uses raw SQL, so must use SQLite, not InMemory

    private DbContextOptions<FlatsDbContext> CreateSqliteOptions()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        return new DbContextOptionsBuilder<FlatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    [Test]
    public async Task BackupFlatsSnapshotsAsync_CreatesBackupTable()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original Listing",
                Address = "Original Address",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(listings);

        // Act
        await repo.BackupFlatsSnapshotsAsync();

        // Assert - backup table should exist with same data
        await using var dbContext = new FlatsDbContext(options);
        var originalCount = await dbContext.FlatsSnapshots.CountAsync();
        originalCount.Should().Be(1);
        (await CountBackupRowsAsync(dbContext)).Should().Be(1);
    }

    [Test]
    public async Task RestoreFlatsSnapshotsAsync_RestoresBackupData()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        // Insert original data
        var originalListings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original Listing",
                Address = "Original Address",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(originalListings);

        // Backup
        await repo.BackupFlatsSnapshotsAsync();

        // Clear the table (simulate failed scrape)
        await using var dbContext = new FlatsDbContext(options);
        await dbContext.FlatsSnapshots.ExecuteDeleteAsync();
        await dbContext.SaveChangesAsync();

        var clearedCount = await dbContext.FlatsSnapshots.CountAsync();
        clearedCount.Should().Be(0);

        // Act - Restore from backup
        await repo.RestoreFlatsSnapshotsAsync();

        // Assert - original data should be restored
        var restoredCount = await dbContext.FlatsSnapshots.CountAsync();
        restoredCount.Should().Be(1);

        var restored = await dbContext.FlatsSnapshots.FirstAsync();
        restored.Address.Should().Be("Original Address");
        restored.PriceEur.Should().Be(500000);
    }


    [Test]
    public async Task BackupRestoreDelete_FullWorkflow_Success()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var originalListings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original",
                Address = "Address 1",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(originalListings);

        // Act - Full workflow: backup -> modify -> restore -> delete backup
        await repo.BackupFlatsSnapshotsAsync();

        // Simulate scrape that clears and inserts new data
        await using var dbContext = new FlatsDbContext(options);
        await dbContext.FlatsSnapshots.ExecuteDeleteAsync();
        await dbContext.SaveChangesAsync();

        var newListings = new List<Flat>
        {
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "New Listing",
                Address = "Address 2",
                PriceEur = 600000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(newListings);

        // Restore (as if scrape failed)
        await repo.RestoreFlatsSnapshotsAsync();

        // Assert - should have original data back
        var restoredCount = await dbContext.FlatsSnapshots.CountAsync();
        restoredCount.Should().Be(1);

        var restored = await dbContext.FlatsSnapshots.FirstAsync();
        restored.Address.Should().Be("Address 1");

        // Delete backup
        await repo.DeleteBackupAsync();
    }

    [Test]
    public async Task BackupRestoreDelete_FullWorkflow_Success_Cleanup()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        var originalListings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original",
                Address = "Address 1",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(originalListings);

        // Act - Full workflow: backup -> scrape succeeds -> delete backup
        await repo.BackupFlatsSnapshotsAsync();

        // Simulate successful scrape (clear and insert new data)
        await using var dbContext = new FlatsDbContext(options);
        await dbContext.FlatsSnapshots.ExecuteDeleteAsync();
        await dbContext.SaveChangesAsync();

        var newListings = new List<Flat>
        {
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "New Listing",
                Address = "Address 2",
                PriceEur = 600000,
                ScrapedAtUtc = DateTime.UtcNow,
                Source = "OhneMakler",
            }
        };
        await repo.UpsertListingsAsync(newListings);

        // Success: delete backup (don't restore)
        await repo.DeleteBackupAsync();

        // Assert - should have new data
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(1);

        var item = await dbContext.FlatsSnapshots.FirstAsync();
        item.Address.Should().Be("Address 2");
    }

    [Test]
    public async Task ClearAsync_DeletesAllRows_AndKeepsBackup()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.UpsertListingsAsync([CreateFlat("1"), CreateFlat("2", source: "ImmobilienScout24")]);
        await repo.BackupFlatsSnapshotsAsync();

        // Act
        await repo.ClearAsync();

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        (await dbContext.FlatsSnapshots.CountAsync()).Should().Be(0);
        (await CountBackupRowsAsync(dbContext)).Should().Be(2);
    }

    [Test]
    public async Task DeleteBackupAsync_DropsBackupTable()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.BackupFlatsSnapshotsAsync();

        // Act
        await repo.DeleteBackupAsync();

        // Assert
        var backupTables = await repoContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'FlatsSnapshots_Backup'")
            .SingleAsync();
        backupTables.Should().Be(0);
    }

    [Test]
    public async Task RestoreFlatsSnapshotsAsync_WithoutBackup_PropagatesError()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);

        // Act
        var act = () => repo.RestoreFlatsSnapshotsAsync();

        // Assert
        await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>()
            .WithMessage("*FlatsSnapshots_Backup*");
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

    private static Task<int> CountBackupRowsAsync(FlatsDbContext dbContext) =>
        dbContext.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM FlatsSnapshots_Backup").SingleAsync();

    [Test]
    public async Task UpsertListingsAsync_UpdatingAFlat_StoresEveryField()
    {
        // Arrange: the old upsert left Title and CommissionPercent unchanged on update
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var repo = new FlatSnapshotRepository(repoContext);
        await repo.UpsertListingsAsync([CreateFlat("1") with { CommissionPercent = 3.57, PriceEur = 300000, LivingAreaSqm = 75 }]);

        // Act
        await repo.UpsertListingsAsync([CreateFlat("1") with { Title = "New title", CommissionPercent = 0, PriceEur = 150000, LivingAreaSqm = 75 }]);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.SingleAsync();
        stored.Title.Should().Be("New title");
        stored.CommissionPercent.Should().Be(0);
        stored.PricePerMeter.Should().Be(2000);
    }

    [Test]
    public async Task UpsertListingsAsync_DatesReadBackAsUtc()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using var repoContext = await CreateContextAsync(options);
        var scrapedAt = new DateTime(2026, 10, 29, 8, 7, 48, DateTimeKind.Utc);

        // Act
        await new FlatSnapshotRepository(repoContext).UpsertListingsAsync([CreateFlat("1") with { ScrapedAtUtc = scrapedAt }]);

        // Assert
        await using var dbContext = new FlatsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.SingleAsync();
        stored.ScrapedAtUtc.Should().Be(scrapedAt);
        stored.ScrapedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        var createdAt = dbContext.Entry(stored).Property<DateTime>("CreatedAtUtc").CurrentValue;
        createdAt.Kind.Should().Be(DateTimeKind.Utc);
        createdAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

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
