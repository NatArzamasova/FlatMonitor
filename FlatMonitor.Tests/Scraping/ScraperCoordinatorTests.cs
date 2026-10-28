using NUnit.Framework;
using FluentAssertions;
using Moq;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;

namespace FlatMonitor.Tests.Scraping;

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
                District = "Oberreut",
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
                District = "Weststadt",
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
                District = "Durlach",
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
                District = "Oberreut",
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
                District = "Durlach",
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
                District = "Weststadt",
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
                District = "Waldstadt",
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

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_ExactMatch()
    {
        // Arrange: flat with exact district name "Oberreut"
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "Oberreut",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be included with normalized district
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(
            l => l.Count == 1 && l[0].District == "Oberreut")), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_CaseInsensitiveExactMatch()
    {
        // Arrange: flat with lowercase district name "oberreut"
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "oberreut",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be included and normalized to "Oberreut"
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(
            l => l.Count == 1 && l[0].District == "Oberreut")), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_SubstringMatch_KAPrefix()
    {
        // Arrange: flat with "KA-Oberreut" contains "Oberreut"
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "KA-Oberreut",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be included and normalized to "Oberreut"
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(
            l => l.Count == 1 && l[0].District == "Oberreut")), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_CaseInsensitiveSubstringMatch()
    {
        // Arrange: flat with "ka-innenstadt-west" contains "Innenstadt-West"
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "ka-innenstadt-west",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be included and normalized to "Innenstadt-West"
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(
            l => l.Count == 1 && l[0].District == "Innenstadt-West")), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_NoMatch_FiltersOut()
    {
        // Arrange: flat with "Bad Schönborn" (not in AllowedDistricts)
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "Bad Schönborn",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be filtered out, no upsert should occur
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>()), Times.Never);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_NullDistrict_FiltersOut()
    {
        // Arrange: flat with null district
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = null,
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be filtered out, no upsert should occur
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>()), Times.Never);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_EmptyDistrict_FiltersOut()
    {
        // Arrange: flat with empty district
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Test",
                District = "",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: flat should be filtered out, no upsert should occur
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>()), Times.Never);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_MixedValidAndInvalid()
    {
        // Arrange: mix of valid and invalid districts
        var listings = new List<Flat>
        {
            new()
            {
                ListingId = "1",
                Url = "/immobilie/1/",
                Title = "Valid 1",
                District = "Oberreut",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            },
            new()
            {
                ListingId = "2",
                Url = "/immobilie/2/",
                Title = "Invalid",
                District = "Bad Schönborn",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            },
            new()
            {
                ListingId = "3",
                Url = "/immobilie/3/",
                Title = "Valid 2",
                District = "KA-Innenstadt-West",
                ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
            }
        };

        _mockScraper1.Setup(s => s.ScrapeAsync())
            .ReturnsAsync(listings);

        var scrapers = new[] { _mockScraper1.Object };
        var coordinator = new ScraperCoordinator(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        await coordinator.ScrapeAllSourcesAsync();

        // Assert: only 2 flats should be included (IDs 1 and 3), with normalized districts
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.Is<List<Flat>>(
            l => l.Count == 2 &&
                 l.Any(f => f.ListingId == "1" && f.District == "Oberreut") &&
                 l.Any(f => f.ListingId == "3" && f.District == "Innenstadt-West"))), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FlatScraper_SavesEachFlatExactlyOnce()
    {
        // Arrange: 25 flats → batches of 20 + 5; one flat outside the allowed districts
        var flats = Enumerable.Range(1, 24).Select(i => FakeFlatScraper.CreateFlat(i.ToString())).ToList();
        flats.Add(FakeFlatScraper.CreateFlat("outside", "Bad Schönborn"));
        var scraper = new FakeFlatScraper("OhneMakler") { Flats = flats };

        var saved = new List<Flat>();
        _mockRepository.Setup(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Flat>, CancellationToken>((batch, _) => saved.AddRange(batch))
            .Returns(Task.CompletedTask);

        var coordinator = new ScraperCoordinator(new IScraper[] { scraper }, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        var total = await coordinator.ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        saved.Select(f => f.ListingId).Should().OnlyHaveUniqueItems().And.HaveCount(24).And.NotContain("outside");
        total.Should().Be(24);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FlatScraper_ReportsSavedCountPerBatchAfterDistrictFilter()
    {
        // Arrange: batch 1 = flats 1..20, batch 2 = flats 21..24 + one outside the allowed districts
        var flats = Enumerable.Range(1, 24).Select(i => FakeFlatScraper.CreateFlat(i.ToString())).ToList();
        flats.Add(FakeFlatScraper.CreateFlat("outside", "Bad Schönborn"));
        var scraper = new FakeFlatScraper("OhneMakler") { Flats = flats };

        var reported = new List<int>();
        var coordinator = new ScraperCoordinator(new IScraper[] { scraper }, _mockRepository.Object, _snapshotWriter, _testDir);
        coordinator.SetOnBatchSavedCallback(count =>
        {
            reported.Add(count);
            return Task.CompletedTask;
        });

        // Act
        var total = await coordinator.ScrapeAllSourcesAsync();

        // Assert
        reported.Should().Equal(20, 4);
        reported.Sum().Should().Be(total);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_OtherScraper_ReportsSavedCountAfterDistrictFilter()
    {
        // Arrange
        _mockScraper1.Setup(s => s.ScrapeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Flat>
            {
                FakeFlatScraper.CreateFlat("1", "Durlach"),
                FakeFlatScraper.CreateFlat("2", "Bad Schönborn"),
                FakeFlatScraper.CreateFlat("3", "KA-Oberreut"),
            });

        var reported = new List<int>();
        var coordinator = new ScraperCoordinator(new[] { _mockScraper1.Object }, _mockRepository.Object, _snapshotWriter, _testDir);
        coordinator.SetOnBatchSavedCallback(count =>
        {
            reported.Add(count);
            return Task.CompletedTask;
        });

        // Act
        var total = await coordinator.ScrapeAllSourcesAsync();

        // Assert
        reported.Should().Equal(2);
        total.Should().Be(2);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenFlatScraperInitializationFails_RecordsSourceErrorAndScrapesOtherSources()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1", "Durlach")] };

        var coordinator = new ScraperCoordinator(new IScraper[] { failing, working }, _mockRepository.Object, _snapshotWriter, _testDir);

        // Act
        var total = await coordinator.ScrapeAllSourcesAsync();

        // Assert
        coordinator.ScraperErrors.Should().ContainSingle()
            .Which.Should().Be("ImmobilienScout24: Login failed");
        total.Should().Be(1);
    }
}
