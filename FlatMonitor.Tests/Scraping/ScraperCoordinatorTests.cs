using System.Runtime.CompilerServices;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;

namespace FlatMonitor.Tests.Scraping;

using static FakeFlatScraper;

[TestFixture]
public class ScraperCoordinatorTests
{
    private Mock<ISnapshotRepository> _mockRepository = null!;
    private List<List<Flat>> _savedBatches = null!;
    private SnapshotWriter _snapshotWriter = null!;
    private string _testDir = null!;

    [SetUp]
    public void Setup()
    {
        _savedBatches = [];
        _mockRepository = new Mock<ISnapshotRepository>();
        _mockRepository.Setup(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Flat>, CancellationToken>((batch, _) => _savedBatches.Add([.. batch]))
            .Returns(Task.CompletedTask);

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

    private List<Flat> Saved => _savedBatches.SelectMany(b => b).ToList();

    private ScraperCoordinator CreateCoordinator(params IScraper[] scrapers) =>
        new(scrapers, _mockRepository.Object, _snapshotWriter, _testDir);

    private string[] SnapshotFiles(string source)
    {
        var dir = Path.Combine(_testDir, "snapshots", source);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.jsonl") : [];
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_ScrapesEachSourceOnceInOrder()
    {
        // Arrange
        var scraper1 = new StubScraper("OhneMakler", CreateFlat("1", "Weststadt"));
        var scraper2 = new StubScraper("ImmobilienScout24", CreateFlat("2", "Durlach"));

        // Act
        var summary = await CreateCoordinator(scraper1, scraper2).ScrapeAllSourcesAsync();

        // Assert
        scraper1.Calls.Should().Be(1);
        scraper2.Calls.Should().Be(1);
        summary.FlatsPerSource.Should().Equal(new Dictionary<string, int> { ["OhneMakler"] = 1, ["ImmobilienScout24"] = 1 });
        summary.TotalFlats.Should().Be(2);
        summary.Success.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_SavesEachSourceSeparately()
    {
        // Arrange
        var scraper1 = new StubScraper("OhneMakler", CreateFlat("1"));
        var scraper2 = new StubScraper("ImmobilienScout24", CreateFlat("2", "Durlach"));

        // Act
        await CreateCoordinator(scraper1, scraper2).ScrapeAllSourcesAsync();

        // Assert
        _savedBatches.Select(b => b.Single().ListingId).Should().Equal("1", "2");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_SavesInBatchesOf20_EachFlatExactlyOnce()
    {
        // Arrange: 25 flats → batches of 20 + 5; one flat outside the allowed districts
        var flats = Enumerable.Range(1, 24).Select(i => CreateFlat(i.ToString())).ToList();
        flats.Add(CreateFlat("outside", "Bad Schönborn"));
        var scraper = new FakeFlatScraper("OhneMakler") { Flats = flats };

        // Act
        var summary = await CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        _savedBatches.Select(b => b.Count).Should().Equal(20, 4);
        Saved.Select(f => f.ListingId).Should().OnlyHaveUniqueItems().And.HaveCount(24).And.NotContain("outside");
        summary.TotalFlats.Should().Be(24);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_ReportsSavedCountPerBatchAfterDistrictFilter()
    {
        // Arrange: batch 1 = flats 1..20, batch 2 = flats 21..24 + one outside the allowed districts
        var flats = Enumerable.Range(1, 24).Select(i => CreateFlat(i.ToString())).ToList();
        flats.Add(CreateFlat("outside", "Bad Schönborn"));
        var scraper = new FakeFlatScraper("OhneMakler") { Flats = flats };
        var reported = new List<int>();

        // Act
        var summary = await CreateCoordinator(scraper).ScrapeAllSourcesAsync(count =>
        {
            reported.Add(count);
            return Task.CompletedTask;
        });

        // Assert
        reported.Should().Equal(20, 4);
        reported.Sum().Should().Be(summary.TotalFlats);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_SavesBatchBeforeTheScraperProducesTheNextOne()
    {
        // Arrange: 21 flats; record how many were saved when the scraper produces flat 21
        int? savedWhenLastFlatProduced = null;
        var flats = Enumerable.Range(1, 21).Select(i => CreateFlat(i.ToString())).ToList();
        var scraper = new FakeFlatScraper("OhneMakler")
        {
            Flats = flats,
            OnEnrich = (flat, _) =>
            {
                if (flat.ListingId == "21")
                    savedWhenLastFlatProduced = Saved.Count;
            },
        };

        // Act
        await CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        savedWhenLastFlatProduced.Should().Be(20);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WritesSnapshotWithAllSavedFlats()
    {
        // Arrange
        var flats = Enumerable.Range(1, 24).Select(i => CreateFlat(i.ToString())).ToList();
        flats.Add(CreateFlat("outside", "Bad Schönborn"));

        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", [.. flats])).ScrapeAllSourcesAsync();

        // Assert
        var file = SnapshotFiles("OhneMakler").Should().ContainSingle().Subject;
        (await File.ReadAllLinesAsync(file)).Should().HaveCount(24);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_SkipsEmptyResults()
    {
        // Act
        var summary = await CreateCoordinator(new StubScraper("OhneMakler"), new StubScraper("ImmobilienScout24"))
            .ScrapeAllSourcesAsync();

        // Assert
        _savedBatches.Should().BeEmpty();
        SnapshotFiles("OhneMakler").Should().BeEmpty();
        SnapshotFiles("ImmobilienScout24").Should().BeEmpty();
        summary.TotalFlats.Should().Be(0);
        summary.Success.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenASourceFails_RecordsErrorAndScrapesOtherSources()
    {
        // Arrange
        var failing = new StubScraper("OhneMakler") { Error = new Exception("Network error") };
        var working = new StubScraper("ImmobilienScout24", CreateFlat("2", "Waldstadt"));

        // Act
        var summary = await CreateCoordinator(failing, working).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: Network error");
        summary.Success.Should().BeFalse();
        Saved.Select(f => f.ListingId).Should().Equal("2");
        SnapshotFiles("ImmobilienScout24").Should().ContainSingle();
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenFlatScraperInitializationFails_RecordsSourceErrorAndScrapesOtherSources()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [CreateFlat("1", "Durlach")] };

        // Act
        var summary = await CreateCoordinator(failing, working).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().ContainSingle().Which.Should().Be("ImmobilienScout24: Login failed");
        summary.TotalFlats.Should().Be(1);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenASourceFailsMidway_KeepsAndCountsTheFlatsAlreadySaved()
    {
        // Arrange: 21 flats, so the first batch of 20 is saved before the failure
        var flats = Enumerable.Range(1, 21).Select(i => CreateFlat(i.ToString())).ToArray();
        var scraper = new StubScraper("OhneMakler", flats) { Error = new HttpRequestException("Detail page timeout") };

        // Act
        var summary = await CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: Detail page timeout");
        summary.FlatsPerSource["OhneMakler"].Should().Be(20);
        Saved.Should().HaveCount(20);
        (await File.ReadAllLinesAsync(SnapshotFiles("OhneMakler").Single())).Should().HaveCount(20);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenCanceled_ThrowsInsteadOfRecordingAnError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var scraper = new FakeFlatScraper("OhneMakler")
        {
            Flats = [CreateFlat("1"), CreateFlat("2")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };
        var next = new StubScraper("ImmobilienScout24", CreateFlat("3"));

        // Act
        var act = () => CreateCoordinator(scraper, next).ScrapeAllSourcesAsync(ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        next.Calls.Should().Be(0);
    }

    [TestCase("Oberreut", "Oberreut", TestName = "District_ExactMatch")]
    [TestCase("oberreut", "Oberreut", TestName = "District_CaseInsensitiveExactMatch")]
    [TestCase("KA-Oberreut", "Oberreut", TestName = "District_SubstringMatch_KAPrefix")]
    [TestCase("ka-innenstadt-west", "Innenstadt-West", TestName = "District_CaseInsensitiveSubstringMatch")]
    public async Task ScrapeAllSourcesAsync_SavesFlatWithNormalizedDistrict(string scraped, string expected)
    {
        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1", scraped))).ScrapeAllSourcesAsync();

        // Assert
        Saved.Should().ContainSingle().Which.District.Should().Be(expected);
    }

    [TestCase("Bad Schönborn", TestName = "District_NoMatch_FiltersOut")]
    [TestCase(null, TestName = "District_Null_FiltersOut")]
    [TestCase("", TestName = "District_Empty_FiltersOut")]
    public async Task ScrapeAllSourcesAsync_FiltersOutFlatOutsideAllowedDistricts(string? scraped)
    {
        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1", scraped!))).ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_FilterByDistrict_MixedValidAndInvalid()
    {
        // Act
        await CreateCoordinator(new StubScraper("OhneMakler",
                CreateFlat("1", "Oberreut"), CreateFlat("2", "Bad Schönborn"), CreateFlat("3", "KA-Innenstadt-West")))
            .ScrapeAllSourcesAsync();

        // Assert: only flats 1 and 3, with normalized districts
        Saved.Select(f => (f.ListingId, f.District)).Should().Equal(("1", "Oberreut"), ("3", "Innenstadt-West"));
    }

    [Test]
    public void Constructor_WithNullScrapers_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator(null!, _mockRepository.Object, _snapshotWriter, _testDir));
    }

    [Test]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator([new StubScraper("OhneMakler")], null!, _snapshotWriter, _testDir));
    }

    [Test]
    public void Constructor_WithNullSnapshotWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator([new StubScraper("OhneMakler")], _mockRepository.Object, null!, _testDir));
    }

    /// <summary>Plain <see cref="IScraper"/>: yields the given flats, then throws <see cref="Error"/> if set.</summary>
    private sealed class StubScraper(string source, params Flat[] flats) : IScraper
    {
        public string Source => source;
        public Exception? Error { get; init; }
        public int Calls { get; private set; }

        public async IAsyncEnumerable<Flat> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls++;
            foreach (var flat in flats)
            {
                await Task.Yield();
                yield return flat;
            }

            if (Error != null)
                throw Error;
        }
    }
}
