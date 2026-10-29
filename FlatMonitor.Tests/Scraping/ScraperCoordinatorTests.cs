using System.Runtime.CompilerServices;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlatMonitor.Tests.Scraping;

using static FakeFlatScraper;

[TestFixture]
public class ScraperCoordinatorTests
{
    private Mock<IFlatRepository> _mockRepository = null!;
    private List<List<Flat>> _savedBatches = null!;
    private List<string> _runCalls = null!;
    private JsonlBackupWriter _backupWriter = null!;
    private string _testDir = null!;

    [SetUp]
    public void Setup()
    {
        _savedBatches = [];
        _runCalls = [];
        _mockRepository = new Mock<IFlatRepository>();
        _mockRepository.Setup(r => r.AddToRunAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Flat>, CancellationToken>((batch, _) => _savedBatches.Add([.. batch]))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.BeginRunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _runCalls.Add("begin")).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.PublishSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((source, _) => _runCalls.Add("publish " + source)).ReturnsAsync(1);
        _mockRepository.Setup(r => r.DiscardRunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _runCalls.Add("discard")).Returns(Task.CompletedTask);

        _backupWriter = new JsonlBackupWriter();
        _logger = new ListLogger<ScraperCoordinator>();

        _testDir = Path.Combine(Path.GetTempPath(), $"ScraperCoordinatorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private List<Flat> Saved => [.. _savedBatches.SelectMany(b => b)];

    private ListLogger<ScraperCoordinator> _logger = null!;

    private ScraperCoordinator CreateCoordinator(params IScraper[] scrapers) =>
        new(scrapers, _mockRepository.Object, _backupWriter, _testDir, _logger);

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
    public async Task ScrapeAllSourcesAsync_WhenASourceFindsNoFlats_RecordsAnError()
    {
        // Arrange
        var empty = new StubScraper("OhneMakler");
        var working = new StubScraper("ImmobilienScout24", CreateFlat("2", "Durlach"));

        // Act
        var summary = await CreateCoordinator(empty, working).ScrapeAllSourcesAsync();

        // Assert
        summary.Success.Should().BeFalse();
        summary.Errors.Should().Equal("OhneMakler: no flats found");
        summary.FlatsPerSource.Should().Equal(new Dictionary<string, int> { ["OhneMakler"] = 0, ["ImmobilienScout24"] = 1 });
        SnapshotFiles("OhneMakler").Should().BeEmpty();
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message == "No flats found for source OhneMakler, keeping its previous flats");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenEveryFlatOfASourceIsOutsideTheTrackedDistricts_RecordsAnError()
    {
        // Act
        var summary = await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1", "Bad Schönborn")))
            .ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: no flats found");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenASourceTimesOut_RecordsAnErrorAndScrapesOtherSources()
    {
        // Arrange: an HttpClient timeout is a TaskCanceledException although nothing was canceled
        var timingOut = new StubScraper("OhneMakler") { Error = new TaskCanceledException("The request timed out", new TimeoutException()) };
        var working = new StubScraper("ImmobilienScout24", CreateFlat("2", "Durlach"));

        // Act
        var summary = await CreateCoordinator(timingOut, working).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: The request timed out");
        working.Calls.Should().Be(1);
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
        var failing = new FakeFlatScraper("ImmobilienScout24") { StartException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [CreateFlat("1", "Durlach")] };

        // Act
        var summary = await CreateCoordinator(failing, working).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().ContainSingle().Which.Should().Be("ImmobilienScout24: Login failed");
        summary.TotalFlats.Should().Be(1);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenASourceFailsMidway_CountsTheStagedFlatsButDoesNotPublishThem()
    {
        // Arrange: 21 flats, so the first batch of 20 is staged before the failure
        var flats = Enumerable.Range(1, 21).Select(i => CreateFlat(i.ToString())).ToArray();
        var scraper = new StubScraper("OhneMakler", flats) { Error = new HttpRequestException("Detail page timeout") };

        // Act
        var summary = await CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: Detail page timeout");
        summary.FlatsPerSource["OhneMakler"].Should().Be(20);
        Saved.Should().HaveCount(20);
        (await File.ReadAllLinesAsync(SnapshotFiles("OhneMakler").Single())).Should().HaveCount(20);
        _runCalls.Should().Equal("begin", "discard");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_PublishesEachSourceThatSucceededRightAfterIt()
    {
        // Arrange
        var publishedBeforeSecondSource = new List<string>();
        var first = new StubScraper("OhneMakler", CreateFlat("1"));
        var failing = new StubScraper("Failing", CreateFlat("2")) { Error = new HttpRequestException("Site down") };
        var empty = new StubScraper("Empty");
        var last = new FakeFlatScraper("ImmobilienScout24")
        {
            Flats = [CreateFlat("3")],
            OnEnrich = (_, _) => publishedBeforeSecondSource.AddRange(_runCalls),
        };

        // Act
        await CreateCoordinator(first, failing, empty, last).ScrapeAllSourcesAsync();

        // Assert
        _runCalls.Should().Equal("begin", "publish OhneMakler", "publish ImmobilienScout24", "discard");
        publishedBeforeSecondSource.Should().Equal("begin", "publish OhneMakler");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_StagesEveryFlatUnderItsScrapersSource()
    {
        // Arrange: a flat that names another source would be staged under a name that is never published
        var scraper = new StubScraper("ImmobilienScout24", CreateFlat("1") with { Source = "Other" });

        // Act
        await CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        Saved.Should().ContainSingle().Which.Source.Should().Be("ImmobilienScout24");
        _runCalls.Should().Contain("publish ImmobilienScout24");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenPublishingFails_RecordsASourceErrorAndScrapesOtherSources()
    {
        // Arrange
        _mockRepository.Setup(r => r.PublishSourceAsync("OhneMakler", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database locked"));
        var next = new StubScraper("ImmobilienScout24", CreateFlat("2"));

        // Act
        var summary = await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1")), next).ScrapeAllSourcesAsync();

        // Assert
        summary.Errors.Should().Equal("OhneMakler: Database locked");
        _runCalls.Should().Equal("begin", "publish ImmobilienScout24", "discard");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenCanceled_EmptiesTheStagingTableWithAnUncanceledToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var scraper = new FakeFlatScraper("OhneMakler")
        {
            Flats = [CreateFlat("1")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };

        // Act
        var act = () => CreateCoordinator(scraper).ScrapeAllSourcesAsync(ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _runCalls.Should().Equal("begin", "discard");
        _mockRepository.Verify(r => r.DiscardRunAsync(It.Is<CancellationToken>(ct => !ct.IsCancellationRequested)), Times.Once);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenEmptyingTheStagingTableFails_StillReturnsTheSummary()
    {
        // Arrange
        _mockRepository.Setup(r => r.DiscardRunAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database locked"));

        // Act
        var summary = await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1"))).ScrapeAllSourcesAsync();

        // Assert
        summary.Success.Should().BeTrue();
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message == "Could not empty the staging table");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenBeginningTheRunFails_ThrowsWithoutScraping()
    {
        // Arrange
        _mockRepository.Setup(r => r.BeginRunAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database locked"));
        var scraper = new StubScraper("OhneMakler", CreateFlat("1"));

        // Act
        var act = () => CreateCoordinator(scraper).ScrapeAllSourcesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database locked");
        scraper.Calls.Should().Be(0);
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

    [Test]
    public async Task ScrapeAllSourcesAsync_NormalizingTheDistrict_KeepsEveryOtherField()
    {
        // Arrange
        var flat = new Flat
        {
            ListingId = "1",
            Url = "https://example.org/1",
            Title = "Title",
            Address = "Address",
            PostalCode = "76199",
            District = "KA-Rüppurr",
            PriceEur = 300000,
            OriginalPriceEur = 320000,
            RoomsCount = 3.5,
            LivingAreaSqm = 75,
            CommissionPercent = 3.57,
            Details = new FlatDetails { Heating = "Gas" },
            ScrapedAtUtc = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
            Source = "ImmobilienScout24",
        };

        // Act
        await CreateCoordinator(new StubScraper("ImmobilienScout24", flat)).ScrapeAllSourcesAsync();

        // Assert
        Saved.Should().ContainSingle().Which.Should().Be(flat with { District = "Rüppurr" });
    }

    [TestCase("Bad Schönborn", TestName = "District_NoMatch_FiltersOut")]
    [TestCase(null, TestName = "District_Null_FiltersOut")]
    [TestCase("", TestName = "District_Empty_FiltersOut")]
    public async Task ScrapeAllSourcesAsync_FiltersOutFlatOutsideAllowedDistricts(string? scraped)
    {
        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("1", scraped!))).ScrapeAllSourcesAsync();

        // Assert
        _mockRepository.Verify(r => r.AddToRunAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_CountsFlatsWithoutADistrictApartFromFlatsOutsideTheTrackedDistricts()
    {
        // Arrange: 7 saved, 2 outside the tracked districts, 1 without a district (10 %: not yet a warning)
        var flats = Enumerable.Range(1, 7).Select(i => CreateFlat(i.ToString())).ToList();
        flats.AddRange([CreateFlat("outside1", "Bad Schönborn"), CreateFlat("outside2", "Ettlingen"), CreateFlat("none", null!)]);

        // Act
        await CreateCoordinator(new StubScraper("ImmobilienScout24", [.. flats])).ScrapeAllSourcesAsync();

        // Assert
        _logger.Messages.Should().Contain("ImmobilienScout24: skipped 2 listings outside the tracked districts")
            .And.Contain("ImmobilienScout24: skipped 1 of 10 listings without a district");
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning && e.Message.Contains("district"));
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenManyFlatsHaveNoDistrict_Warns()
    {
        // Arrange: 2 of 10 without a district, e.g. their detail pages failed
        var flats = Enumerable.Range(1, 8).Select(i => CreateFlat(i.ToString())).ToList();
        flats.AddRange([CreateFlat("none1", null!), CreateFlat("none2", "")]);

        // Act
        await CreateCoordinator(new StubScraper("ImmobilienScout24", [.. flats])).ScrapeAllSourcesAsync();

        // Assert
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("district"))
            .Which.Message.Should().Be("ImmobilienScout24: skipped 2 of 10 listings without a district");
        _logger.Messages.Should().NotContain(m => m.Contains("outside the tracked districts"));
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_LogsParseHealthPerSource()
    {
        // Arrange: of 5 saved flats, 1 without price (20 %: not yet a warning); the flat outside is not counted
        var flats = Enumerable.Range(1, 5)
            .Select(i => CreateFlat(i.ToString()) with { PriceEur = 300000, LivingAreaSqm = 75, RoomsCount = 3 })
            .ToList();
        flats[0] = flats[0] with { PriceEur = null };
        flats.Add(CreateFlat("outside", "Bad Schönborn"));

        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", [.. flats])).ScrapeAllSourcesAsync();

        // Assert
        var entry = _logger.Entries.Should().ContainSingle(e => e.Message.Contains("parse health")).Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Be("OhneMakler: parse health: of 5 saved flats, 1 without price, 0 without area, 0 without rooms");
    }

    [TestCase(nameof(Flat.PriceEur))]
    [TestCase(nameof(Flat.LivingAreaSqm))]
    [TestCase(nameof(Flat.RoomsCount))]
    public async Task ScrapeAllSourcesAsync_WhenManyFlatsLackAValue_WarnsAboutParseHealth(string missing)
    {
        // Arrange: 2 of 5 flats lack the value, as when the markup changed
        var flats = Enumerable.Range(1, 5)
            .Select(i => CreateFlat(i.ToString()) with { PriceEur = 300000, LivingAreaSqm = 75, RoomsCount = 3 })
            .Select((f, i) => i >= 2 ? f : missing switch
            {
                nameof(Flat.PriceEur) => f with { PriceEur = null },
                nameof(Flat.LivingAreaSqm) => f with { LivingAreaSqm = null },
                _ => f with { RoomsCount = null },
            })
            .ToArray();

        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", flats)).ScrapeAllSourcesAsync();

        // Assert
        _logger.Entries.Should().ContainSingle(e => e.Message.Contains("parse health"))
            .Which.Level.Should().Be(LogLevel.Warning);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_ASourceWithoutSavedFlats_LogsNoParseHealth()
    {
        await CreateCoordinator(new StubScraper("OhneMakler", CreateFlat("outside", "Bad Schönborn"))).ScrapeAllSourcesAsync();

        _logger.Messages.Should().NotContain(m => m.Contains("parse health"));
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
            new ScraperCoordinator(null!, _mockRepository.Object, _backupWriter, _testDir, NullLogger<ScraperCoordinator>.Instance));
    }

    [Test]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator([new StubScraper("OhneMakler")], null!, _backupWriter, _testDir, NullLogger<ScraperCoordinator>.Instance));
    }

    [Test]
    public void Constructor_WithNullBackupWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator([new StubScraper("OhneMakler")], _mockRepository.Object, null!, _testDir, NullLogger<ScraperCoordinator>.Instance));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScraperCoordinator([new StubScraper("OhneMakler")], _mockRepository.Object, _backupWriter, _testDir, null!));
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_LogsEachSavedBatchAtInformation()
    {
        // Arrange
        var flats = Enumerable.Range(1, 25).Select(i => CreateFlat(i.ToString())).ToArray();

        // Act
        await CreateCoordinator(new StubScraper("OhneMakler", flats)).ScrapeAllSourcesAsync();

        // Assert
        _logger.Entries.Where(e => e.Level == LogLevel.Information && e.Message.Contains("saved"))
            .Select(e => e.Message)
            .Should().Equal("OhneMakler: saved 20 flats (20 so far)", "OhneMakler: saved 5 flats (25 so far)");
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenASourceFails_LogsErrorWithTheException()
    {
        // Arrange
        var error = new HttpRequestException("Network error");

        // Act
        await CreateCoordinator(new StubScraper("OhneMakler") { Error = error }).ScrapeAllSourcesAsync();

        // Assert
        var entry = _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error).Subject;
        entry.Message.Should().Be("Error scraping OhneMakler, keeping its previous flats");
        entry.Exception.Should().BeSameAs(error);
    }

    [Test]
    public async Task ScrapeAllSourcesAsync_WhenCanceled_LogsWarningNotError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var scraper = new FakeFlatScraper("OhneMakler")
        {
            Flats = [CreateFlat("1")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };

        // Act
        var act = () => CreateCoordinator(scraper).ScrapeAllSourcesAsync(ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message == "Scraping canceled for OhneMakler");
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
