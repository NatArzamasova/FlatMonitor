using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Services;

/// <summary>
/// A real <see cref="ScrapeExecutionService"/> and coordinator over fake scrapers; the history and flat repositories
/// are mocks, so the test sees exactly which history writes a run makes.
/// </summary>
[TestFixture]
public class ScrapeRunServiceTests
{
    private const int RunId = 7;

    private Mock<IScrapingHistoryRepository> _history = null!;
    private Mock<IFlatAggregationRepository> _aggregations = null!;
    private List<string> _historyCalls = null!;
    private ListLogger<ScrapeRunService> _logger = null!;
    private string _dataDir = null!;

    [SetUp]
    public void Setup()
    {
        _historyCalls = [];
        _history = new Mock<IScrapingHistoryRepository>();
        _history.Setup(h => h.StartAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _historyCalls.Add("start")).ReturnsAsync(RunId);
        _history.Setup(h => h.AddProgressAsync(RunId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, CancellationToken>((_, flats, _) => _historyCalls.Add($"progress {flats}")).Returns(Task.CompletedTask);
        _history.Setup(h => h.CompleteAsync(RunId, It.IsAny<ScrapingStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<int, ScrapingStatus, string?, CancellationToken>((_, status, error, _) => _historyCalls.Add($"complete {status} {error}".TrimEnd()))
            .Returns(Task.CompletedTask);
        _aggregations = new Mock<IFlatAggregationRepository>();
        _logger = new ListLogger<ScrapeRunService>();
        _dataDir = Path.Combine(Path.GetTempPath(), $"ScrapeRunServiceTests_{Guid.NewGuid()}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public async Task RunAsync_WhenAllSourcesSucceed_RecordsStartProgressAndSuccess()
    {
        // Arrange: 25 flats, saved in batches of 20 and 5
        var flats = Enumerable.Range(1, 25).Select(i => FakeFlatScraper.CreateFlat(i.ToString())).ToList();

        // Act
        var result = await CreateService(new FakeFlatScraper("OhneMakler") { Flats = flats }).RunAsync();

        // Assert
        result.Status.Should().Be(ScrapingStatus.Successful);
        result.Summary!.TotalFlats.Should().Be(25);
        _historyCalls.Should().Equal("start", "progress 20", "progress 5", "complete Successful");
    }

    [Test]
    public async Task RunAsync_WhenASourceFails_RecordsPartialFailedWithTheErrors()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { StartException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] };

        // Act
        var result = await CreateService(failing, working).RunAsync();

        // Assert
        result.Status.Should().Be(ScrapingStatus.PartialFailed);
        result.ErrorMessage.Should().Be("ImmobilienScout24: Login failed");
        _historyCalls.Should().Equal("start", "progress 1", "complete PartialFailed ImmobilienScout24: Login failed");
    }

    [Test]
    public async Task RunAsync_WhenTheRunThrows_RecordsFailedWithTheMessage()
    {
        // Arrange
        _aggregations.Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Disk full"));

        // Act
        var result = await CreateService(new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] }).RunAsync();

        // Assert
        result.Status.Should().Be(ScrapingStatus.Failed);
        result.ErrorMessage.Should().Be("Disk full");
        _historyCalls.Should().EndWith("complete Failed Disk full");
        _logger.Entries.Should().Contain(e => e.Message == $"Scrape run {RunId} failed" && e.Exception!.Message == "Disk full");
    }

    [Test]
    public async Task RunAsync_WhenCanceled_RecordsCanceledWithAnUncanceledToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var scraper = new FakeFlatScraper("OhneMakler")
        {
            Flats = [FakeFlatScraper.CreateFlat("1")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };

        // Act
        var result = await CreateService(scraper).RunAsync(cts.Token);

        // Assert
        result.Status.Should().Be(ScrapingStatus.Canceled);
        _historyCalls.Should().Equal("start", "complete Canceled");
        _history.Verify(h => h.CompleteAsync(RunId, ScrapingStatus.Canceled, null,
            It.Is<CancellationToken>(ct => !ct.IsCancellationRequested)), Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenATimeoutLooksLikeACancel_RecordsFailedNotCanceled()
    {
        // Arrange: a TaskCanceledException without a canceled token is a failure, e.g. an HttpClient timeout
        _aggregations.Setup(a => a.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request timed out", new TimeoutException()));

        // Act
        var result = await CreateService(new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] }).RunAsync();

        // Assert
        result.Status.Should().Be(ScrapingStatus.Failed);
    }

    [Test]
    public async Task RunAsync_WhenCanceledWhileStarting_RecordsNothing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _history.Setup(h => h.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException(cts.Token));
        var scraper = new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] };

        // Act
        var result = await CreateService(scraper).RunAsync(cts.Token);

        // Assert
        result.Status.Should().Be(ScrapingStatus.Canceled);
        _history.Verify(h => h.CompleteAsync(It.IsAny<int>(), It.IsAny<ScrapingStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        scraper.SessionStarted.Should().BeFalse("the scrape never started");
    }

    private ScrapeRunService CreateService(params IScraper[] scrapers)
    {
        var coordinator = new ScraperCoordinator(
            scrapers, new Mock<ISnapshotRepository>().Object, new SnapshotWriter(), _dataDir, NullLogger<ScraperCoordinator>.Instance);
        var execution = new ScrapeExecutionService(_aggregations.Object, coordinator, NullLogger<ScrapeExecutionService>.Instance);
        return new ScrapeRunService(_history.Object, execution, _logger);
    }
}
