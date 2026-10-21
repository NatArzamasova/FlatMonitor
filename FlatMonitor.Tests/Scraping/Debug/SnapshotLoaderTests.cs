using NUnit.Framework;
using FluentAssertions;
using System.Text.Json;
using workInProgress.Json;
using workInProgress.Models;
using workInProgress.Storage;
using FlatMonitor.Scraping.Debug;

namespace workInProgress.Tests.Scraping.Debug;

[TestFixture]
public class SnapshotLoaderTests
{
    private string _testDir = null!;
    private string _snapshotsDir = null!;

    [SetUp]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"OhneMaklerTests_{Guid.NewGuid()}");
        _snapshotsDir = Path.Combine(_testDir, "snapshots");
        Directory.CreateDirectory(_snapshotsDir);
        // Create default source subdirectory for backward compatibility
        Directory.CreateDirectory(Path.Combine(_snapshotsDir, "default"));
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Test]
    public void LoadAllSnapshots_WithNoSnapshotsDirectory_ReturnsEmptyList()
    {
        // Arrange
        var testDirWithoutSnapshots = Path.Combine(Path.GetTempPath(), $"OhneMaklerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDirWithoutSnapshots);

        var loader = new SnapshotLoader();

        try
        {
            // Act
            var listings = loader.LoadAllSnapshots(testDirWithoutSnapshots);

            // Assert
            listings.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(testDirWithoutSnapshots, recursive: true);
        }
    }

    [Test]
    public void LoadAllSnapshots_WithEmptySnapshotsDirectory_ReturnsEmptyList()
    {
        // Arrange
        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().BeEmpty();
    }

    [Test]
    public void LoadAllSnapshots_WithSingleSnapshotFile_ReturnsAllListings()
    {
        // Arrange
        var listing1 = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "Test Listing 1",
            Address = "Address 1",
            PostalCode = "76131",
            District = "Innenstadt",
            PriceEur = 500000,
            RoomsCount = 3,
            LivingAreaSqm = 85,
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var listing2 = new Flat
        {
            ListingId = "2",
            Url = "/immobilie/2/",
            Title = "Test Listing 2",
            Address = "Address 2",
            PostalCode = "76227",
            District = "Durlach",
            PriceEur = 600000,
            RoomsCount = 4,
            LivingAreaSqm = 100,
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var sourceDir = Path.Combine(_snapshotsDir, "OhneMakler");
        Directory.CreateDirectory(sourceDir);
        var snapshotPath = Path.Combine(sourceDir, "2026-10-12T10-00-00.jsonl");
        var lines = new[]
        {
            JsonSerializer.Serialize(listing1, JsonOptions.Snapshot),
            JsonSerializer.Serialize(listing2, JsonOptions.Snapshot)
        };
        File.WriteAllLines(snapshotPath, lines);

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(2);
        listings[0].ListingId.Should().Be("1");
        listings[0].Title.Should().Be("Test Listing 1");
        listings[1].ListingId.Should().Be("2");
        listings[1].Title.Should().Be("Test Listing 2");
    }

    [Test]
    public void LoadAllSnapshots_WithMultipleSnapshotFiles_LoadsInChronologicalOrder()
    {
        // Arrange
        var listing1 = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "From File 1",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var listing2 = new Flat
        {
            ListingId = "2",
            Url = "/immobilie/2/",
            Title = "From File 2",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var sourceDir = Path.Combine(_snapshotsDir, "OhneMakler");
        Directory.CreateDirectory(sourceDir);
        var file1Path = Path.Combine(sourceDir, "2026-10-11T10-00-00.jsonl");
        File.WriteAllText(file1Path, JsonSerializer.Serialize(listing1, JsonOptions.Snapshot));

        var file2Path = Path.Combine(sourceDir, "2026-10-12T10-00-00.jsonl");
        File.WriteAllText(file2Path, JsonSerializer.Serialize(listing2, JsonOptions.Snapshot));

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(2);
        listings[0].Title.Should().Be("From File 1");
        listings[1].Title.Should().Be("From File 2");
    }

    [Test]
    public void LoadAllSnapshots_SkipsMalformedLinesWithoutThrowing()
    {
        // Arrange
        var validListing = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "Valid",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var sourceDir = Path.Combine(_snapshotsDir, "OhneMakler");
        Directory.CreateDirectory(sourceDir);
        var snapshotPath = Path.Combine(sourceDir, "2026-10-12T10-00-00.jsonl");
        var lines = new[]
        {
            JsonSerializer.Serialize(validListing, JsonOptions.Snapshot),
            "this is not valid json {",
            "",
            "another { invalid"
        };
        File.WriteAllLines(snapshotPath, lines);

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(1);
        listings[0].ListingId.Should().Be("1");
    }

    [Test]
    public void LoadAllSnapshots_WithNullFields_PreservesNulls()
    {
        // Arrange
        var listing = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = null,
            Address = null,
            PriceEur = null,
            Details = null,
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var sourceDir = Path.Combine(_snapshotsDir, "OhneMakler");
        Directory.CreateDirectory(sourceDir);
        var snapshotPath = Path.Combine(sourceDir, "2026-10-12T10-00-00.jsonl");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(listing, JsonOptions.Snapshot));

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(1);
        listings[0].Title.Should().BeNull();
        listings[0].Address.Should().BeNull();
        listings[0].PriceEur.Should().BeNull();
        listings[0].Details.Should().BeNull();
    }

    [Test]
    public void LoadAllSnapshots_WithDetailsObject_PreservesNestedData()
    {
        // Arrange
        var listing = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "Test",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            Details = new FlatDetails
            {
                PropertyType = "Wohnung",
                BuildYear = 2010,
                Floor = "2",
                Condition = "Renoviert",
                Heating = "Gas",
                ServiceChargeEur = 150,
            }
        };

        var sourceDir = Path.Combine(_snapshotsDir, "OhneMakler");
        Directory.CreateDirectory(sourceDir);
        var snapshotPath = Path.Combine(sourceDir, "2026-10-12T10-00-00.jsonl");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(listing, JsonOptions.Snapshot));

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(1);
        var storedListing = listings[0];
        storedListing.Details.Should().NotBeNull();
        storedListing.Details!.PropertyType.Should().Be("Wohnung");
        storedListing.Details.BuildYear.Should().Be(2010);
        storedListing.Details.Heating.Should().Be("Gas");
    }

    [Test]
    public void LoadAllSnapshots_WithMultipleSources_LoadsFromAllSourceDirectories()
    {
        // Arrange
        var listing1 = new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "From OhneMakler",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var listing2 = new Flat
        {
            ListingId = "2",
            Url = "/property/2",
            Title = "From ImmobilienScout24",
            ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        var ohneDir = Path.Combine(_snapshotsDir, "OhneMakler");
        var scoutDir = Path.Combine(_snapshotsDir, "ImmobilienScout24");
        Directory.CreateDirectory(ohneDir);
        Directory.CreateDirectory(scoutDir);

        var file1 = Path.Combine(ohneDir, "2026-10-12T10-00-00.jsonl");
        var file2 = Path.Combine(scoutDir, "2026-10-12T10-00-00.jsonl");
        File.WriteAllText(file1, JsonSerializer.Serialize(listing1, JsonOptions.Snapshot));
        File.WriteAllText(file2, JsonSerializer.Serialize(listing2, JsonOptions.Snapshot));

        var loader = new SnapshotLoader();

        // Act
        var listings = loader.LoadAllSnapshots(_testDir);

        // Assert
        listings.Should().HaveCount(2);
        listings.Should().Contain(l => l.Title == "From OhneMakler");
        listings.Should().Contain(l => l.Title == "From ImmobilienScout24");
    }
}
