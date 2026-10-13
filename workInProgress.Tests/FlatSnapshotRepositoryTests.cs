using NUnit.Framework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Storage;
using workInProgress.Scraping.OhneMakler;

namespace workInProgress.Tests;

[TestFixture]
public class FlatSnapshotRepositoryTests
{
    private DbContextOptions<ListingsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ListingsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Test]
    public async Task InitializeAsync_CreatesSchema()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);

        // Act
        await repo.InitializeAsync();

        // Assert - verify Listings table exists by querying
        await using var dbContext = new ListingsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(0);
    }

    [Test]
    public async Task UpsertListingsAsync_InsertsNewListing()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

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
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            }
        };

        // Act
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new ListingsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Test Listing");
        stored.Address.Should().Be("123 Main St");
        stored.PriceEur.Should().Be(500000);
        stored.Source.Should().Be("OhneMakler");
    }

    [Test]
    public async Task UpsertListingsAsync_WithDetails_PersistsDetailFields()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                Address = "Test Address",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
                Details = new Details
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
        await using var dbContext = new ListingsDbContext(options);
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
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                Address = "Test Address",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
                Details = null,
            }
        };

        // Act
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new ListingsDbContext(options);
        var stored = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.ListingId == "1");
        stored.Should().NotBeNull();
        stored!.Details.Should().BeNull();
    }

    [Test]
    public async Task UpsertListingsAsync_CalledTwiceWithSameListingId_UpdatesExistingRow()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

        var listing1 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Original Title",
                Address = "Original Address",
                PriceEur = 500000,
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
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
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            }
        };

        // Act
        await repo.UpsertListingsAsync(listing2);

        // Assert - row count should stay 1
        await using var dbContext = new ListingsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(1);

        var stored = await dbContext.FlatsSnapshots.FirstAsync();
        stored.Title.Should().Be("Updated Title");
        stored.PriceEur.Should().Be(550000);
    }

    [Test]
    public async Task UpsertListingsAsync_WithMultipleListings_InsertsAllRows()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Listing 1",
                Address = "Address 1",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            },
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "Listing 2",
                Address = "Address 2",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            },
            new()
            {
                ListingId = "3",
                Url = "/immobilie/3/",
                Title = "Listing 3",
                Address = "Address 3",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            }
        };

        // Act
        await repo.UpsertListingsAsync(listings);

        // Assert
        await using var dbContext = new ListingsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(3);
    }

    [Test]
    public async Task UpsertListingsAsync_WithMultipleSources_PersistsBothAsSeperateRows()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        await using var repo = new FlatSnapshotRepository(options);
        await repo.InitializeAsync();

        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "123",
                Url = "/immobilie/123/",
                Title = "From OhneMakler",
                Address = "Address A",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "OhneMakler",
            },
            new()
            {
                ListingId = "123",
                Url = "/property/123",
                Title = "From ImmobilienScout24",
                Address = "Address B",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Source = "ImmobilienScout24",
            }
        };

        // Act
        await repo.UpsertListingsAsync(listings);

        // Assert - both rows with same ListingId but different Source should exist
        await using var dbContext = new ListingsDbContext(options);
        var count = await dbContext.FlatsSnapshots.CountAsync();
        count.Should().Be(2);

        var ohmRow = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.Source == "OhneMakler" && l.ListingId == "123");
        ohmRow.Should().NotBeNull();
        ohmRow!.Title.Should().Be("From OhneMakler");

        var scoutRow = await dbContext.FlatsSnapshots.FirstOrDefaultAsync(l => l.Source == "ImmobilienScout24" && l.ListingId == "123");
        scoutRow.Should().NotBeNull();
        scoutRow!.Title.Should().Be("From ImmobilienScout24");
    }
}
