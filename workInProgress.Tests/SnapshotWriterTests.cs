
using NUnit.Framework;
using FluentAssertions;
using System.Text.Json;
using workInProgress.Models;
using workInProgress.Storage;
using workInProgress.Scraping.OhneMakler;

namespace workInProgress.Tests;
[TestFixture]
public class SnapshotWriterTests
{
    private SnapshotWriter _writer = null!;
    private string _testDir = null!;

    [SetUp]
    public void Setup()
    {
        _writer = new SnapshotWriter();
        _testDir = Path.Combine(Path.GetTempPath(), $"OhneMaklerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Test]
    public async Task WriteSnapshotAsync_CreatesDataSnapshotsDirectory()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test Listing",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        var snapshotsDir = Path.Combine(_testDir, "snapshots", "OhneMakler");
        Directory.Exists(snapshotsDir).Should().BeTrue();
    }

    [Test]
    public async Task WriteSnapshotAsync_CreatesFileWithUtcTimestampInName()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test Listing",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        File.Exists(path).Should().BeTrue();
        path.Should().Contain("snapshots");
        path.Should().Contain("OhneMakler");
        path.Should().EndWith(".jsonl");
        // Filename should match timestamp format: YYYY-MM-DDTHH-mm-ss
        var filename = Path.GetFileNameWithoutExtension(path);
        filename.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}$");
    }

    [Test]
    public async Task WriteSnapshotAsync_WritesOneJsonObjectPerLine()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Listing 1",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            },
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "Listing 2",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        var lines = File.ReadAllLines(path);
        lines.Length.Should().Be(2);

        foreach (var line in lines)
        {
            // Each line should be valid JSON
            var doc = JsonDocument.Parse(line);
            doc.Should().NotBeNull();
        }
    }

    [Test]
    public async Task WriteSnapshotAsync_SerializesWithPascalCase()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                PriceEur = 100000,
                RoomsCount = 3,
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        var content = File.ReadAllText(path);
        content.Should().Contain("ListingId");
        content.Should().Contain("PriceEur");
        content.Should().Contain("RoomsCount");
        content.Should().NotContain("listingId");
        content.Should().NotContain("priceEur");
    }

    [Test]
    public async Task WriteSnapshotAsync_SerializesNullAsJsonNull()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = null,
                Address = null,
                PriceEur = null,
                Details = null,
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        var content = File.ReadAllText(path);
        content.Should().Contain("\"Title\":null");
        content.Should().Contain("\"Address\":null");
        content.Should().Contain("\"PriceEur\":null");
        content.Should().Contain("\"Details\":null");
    }

    [Test]
    public async Task WriteSnapshotAsync_WithDetailsObject_IncludesAllDetailsFields()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                Details = new Details
                {
                    PropertyType = "Wohnung",
                    BuildYear = 2020,
                    Condition = "Neu",
                    Heating = "Gas",
                }
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        var content = File.ReadAllText(path);
        content.Should().Contain("\"PropertyType\":\"Wohnung\"");
        content.Should().Contain("\"BuildYear\":2020");
        content.Should().Contain("\"Condition\":\"Neu\"");
        content.Should().Contain("\"Heating\":\"Gas\"");
    }

    [Test]
    public async Task WriteSnapshotAsync_CreatesNewFileEachTime()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path1 = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");
        await Task.Delay(1100); // Ensure different timestamp
        var path2 = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        path1.Should().NotBe(path2);
        File.Exists(path1).Should().BeTrue();
        File.Exists(path2).Should().BeTrue();
    }

    [Test]
    public async Task WriteSnapshotAsync_WithEmptyList_CreatesEmptyFile()
    {
        // Arrange
        var listings = new List<Flat>();

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");

        // Assert
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().BeEmpty();
    }

    [Test]
    public async Task WriteSnapshotAsync_WithDifferentSources_OrganizesIntoSeparateDirectories()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path1 = await _writer.WriteSnapshotAsync(listings, _testDir, "OhneMakler");
        var path2 = await _writer.WriteSnapshotAsync(listings, _testDir, "ImmobilienScout24");

        // Assert
        path1.Should().Contain("snapshots" + Path.DirectorySeparatorChar + "OhneMakler");
        path2.Should().Contain("snapshots" + Path.DirectorySeparatorChar + "ImmobilienScout24");
        File.Exists(path1).Should().BeTrue();
        File.Exists(path2).Should().BeTrue();
    }

    [Test]
    public async Task WriteSnapshotAsync_WithNullSource_UsesDefaultSource()
    {
        // Arrange
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        // Act
        var path = await _writer.WriteSnapshotAsync(listings, _testDir, source: null);

        // Assert
        path.Should().Contain("snapshots" + Path.DirectorySeparatorChar + "default");
        File.Exists(path).Should().BeTrue();
    }
}
