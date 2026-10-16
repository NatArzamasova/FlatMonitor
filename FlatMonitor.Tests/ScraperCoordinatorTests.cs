using NUnit.Framework;
using FluentAssertions;
using Moq;
using workInProgress.Models;
using workInProgress.Scraping;
using workInProgress.Storage;

namespace workInProgress.Tests;

[TestFixture]
public class ScraperCoordinatorTests
{
    private Mock<IScraper> _mockScraper1 = null!;
    private Mock<IScraper> _mockScraper2 = null!;
    private Mock<ISnapshotRepository> _mockRepository = null!;
    private SnapshotWriter _snapshotWriter = null!;
    private string _testDir = null!;

    [SetUp]
    public void Setup()
    {
        _mockScraper1 = new Mock<IScraper>();
        _mockScraper1.Setup(s => s.Source).Returns("OhneMakler");

        _mockScraper2 = new Mock<IScraper>();
        _mockScraper2.Setup(s => s.Source).Returns("ImmobilienScout24");

        _mockRepository = new Mock<ISnapshotRepository>();
        _mockRepository.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.DisposeAsync())
            .Returns(new ValueTask());

        _snapshotWriter = new SnapshotWriter();

        _testDir = Path.Combine(Path.GetTempPath(), $"ScraperCoordinatorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_InitializesRepositoryOnce()
    {
        // Arrange
        var listings1 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test 1",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings1);
        _mockScraper2.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(new List<Flat>());

        var scrapers = new[] { _mockScraper1.Object, _mockScraper2.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_CallsScraperForEachSource()
    {
        // Arrange
        var listings1 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test 1",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        var listings2 = new List<Flat>
        {
            new()
            {
                ListingId = "2",
                Url = "/property/2",
                Title = "Test 2",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings1);
        _mockScraper2.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings2);

        var scrapers = new[] { _mockScraper1.Object, _mockScraper2.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        _mockScraper1.Verify(s => s.ScrapeAsync(), Times.Once);
        _mockScraper2.Verify(s => s.ScrapeAsync(), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_UpsertListingsForEachSource()
    {
        // Arrange
        var listings1 = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test 1",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        var listings2 = new List<Flat>
        {
            new()
            {
                ListingId = "2",
                Url = "/property/2",
                Title = "Test 2",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings1);
        _mockScraper2.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings2);

        var scrapers = new[] { _mockScraper1.Object, _mockScraper2.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(l => l.Count == 1 && l[0].ListingId == "1")), Times.Once);
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(l => l.Count == 1 && l[0].ListingId == "2")), Times.Once);
    }

[Test]
    public async Task ScrapeAllSourcesAsync_WritesSnapshotWithSourceName()
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

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        var sourceDir = Path.Combine(_testDir, "snapshots", "OhneMakler");
        Directory.Exists(sourceDir).Should().BeTrue();
        var snapshotFiles = Directory.GetFiles(sourceDir, "*.jsonl");
        snapshotFiles.Should().HaveCount(1);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_SkipsEmptyResults()
    {
        // Arrange
        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(new List<Flat>());
        _mockScraper2.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(new List<Flat>());

        var scrapers = new[] { _mockScraper1.Object, _mockScraper2.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>()), Times.Never);
        // Verify no snapshot files were created
        var snapshotsDir = Path.Combine(_testDir, "snapshots");
        if (Directory.Exists(snapshotsDir))
        {
            var files = Directory.GetFiles(snapshotsDir, "*.jsonl", SearchOption.AllDirectories);
            files.Should().BeEmpty();
        }
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_ContinuesOnSourceError()
    {
        // Arrange
        var listings2 = new List<Flat>
        {
            new()
            {
                ListingId = "2",
                Url = "/property/2",
                Title = "Test 2",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ThrowsAsync(new Exception("Network error"));
        _mockScraper2.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings2);

        var scrapers = new[] { _mockScraper1.Object, _mockScraper2.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert
        // Scraper 2 should still be called and upserted despite scraper 1 failing
        _mockScraper2.Verify(s => s.ScrapeAsync(), Times.Once);
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(l => l.Count == 1 && l[0].ListingId == "2")), Times.Once);
        // Verify snapshot was created for scraper 2
        var scoutDir = Path.Combine(_testDir, "snapshots", "ImmobilienScout24");
        Directory.Exists(scoutDir).Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_DisposesRepositoryOnCompletion()
    {
        // Arrange
        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(new List<Flat>());

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.DisposeAsync();

        // Assert
        _mockRepository.Verify(r => r.DisposeAsync(), Times.Once);
    }

    [Test]
    public void Constructor_WithNullScrapers_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator(null!, _mockRepository.Object, _snapshotWriter, _testDir));
    }

    [Test]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator(new[] { _mockScraper1.Object }, null!, _snapshotWriter, _testDir));
    }

    [Test]
    public void Constructor_WithNullSnapshotWriter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator(new[] { _mockScraper1.Object }, _mockRepository.Object, null!, _testDir));
    }
}
