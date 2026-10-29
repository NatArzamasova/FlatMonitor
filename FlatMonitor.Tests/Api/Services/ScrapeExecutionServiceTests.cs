using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;

namespace FlatMonitor.Tests.Api.Services;

[TestFixture]
public class ScrapeSummaryTests
{
    [Test]
    public void TotalFlats_SumsAllSources()
    {
        var summary = new ScrapeSummary(new Dictionary<string, int> { ["OhneMakler"] = 3, ["ImmobilienScout24"] = 4 }, []);

        summary.TotalFlats.Should().Be(7);
        summary.Success.Should().BeTrue();
    }

    [Test]
    public void Success_IsFalseWhenAnySourceFailed()
    {
        var summary = new ScrapeSummary(new Dictionary<string, int> { ["OhneMakler"] = 3 }, ["ImmobilienScout24: Login failed"]);

        summary.Success.Should().BeFalse();
    }
}

[TestFixture]
public class ScrapeExecutionServiceTests
{
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private Mock<ILogger<ScrapeExecutionService>> _mockLogger = null!;
    private ScraperCoordinator _coordinator = null!;

    [SetUp]
    public void Setup()
    {
        _mockAggregationRepository = new Mock<IFlatAggregationRepository>();
        _mockLogger = new Mock<ILogger<ScrapeExecutionService>>();
        _coordinator = new ScraperCoordinator(
            Array.Empty<IScraper>(),
            new Mock<IFlatRepository>().Object,
            new JsonlBackupWriter(),
            Path.GetTempPath(),
            NullLogger<ScraperCoordinator>.Instance);
    }

    [Test]
    public void Constructor_RequiresAggregationRepository()
    {
        var action = () => new ScrapeExecutionService(null!, _coordinator, _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresCoordinator()
    {
        var action = () => new ScrapeExecutionService(_mockAggregationRepository.Object, null!, _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresLogger()
    {
        var action = () => new ScrapeExecutionService(_mockAggregationRepository.Object, _coordinator, null!);

        action.Should().Throw<ArgumentNullException>();
    }
}

[TestFixture]
public class ScrapeExecutionServiceExecuteTests
{
    private Mock<IFlatRepository> _mockFlatRepository = null!;
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private ListLogger<ScrapeExecutionService> _logger = null!;
    private List<string> _calls = null!;
    private string _dataDir = null!;

    [SetUp]
    public void Setup()
    {
        // Every repository call completes without doing anything; the calls are recorded in order
        _calls = [];
        _mockFlatRepository = new Mock<IFlatRepository>();
        _mockFlatRepository.Setup(r => r.BeginRunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("begin")).Returns(Task.CompletedTask);
        _mockFlatRepository.Setup(r => r.AddToRunAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("add")).Returns(Task.CompletedTask);
        _mockFlatRepository.Setup(r => r.PublishSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((source, _) => _calls.Add($"publish {source}")).ReturnsAsync(1);
        _mockFlatRepository.Setup(r => r.DiscardRunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("discard")).Returns(Task.CompletedTask);
        _mockAggregationRepository = new Mock<IFlatAggregationRepository>();
        _mockAggregationRepository.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("refresh")).Returns(Task.CompletedTask);
        _logger = new ListLogger<ScrapeExecutionService>();
        _dataDir = Path.Combine(Path.GetTempPath(), $"ScrapeExecutionServiceExecuteTests_{Guid.NewGuid()}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public async Task ExecuteAsync_StagesPublishesAndThenRefreshesTheAggregations()
    {
        // Arrange
        var service = CreateService(new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();
        _calls.Should().Equal("begin", "add", "publish OhneMakler", "discard", "refresh");
        _logger.Messages.Should().Equal("Refreshing flat aggregations");
    }

    [Test]
    public async Task ExecuteAsync_ReportsEachSavedBatchToTheCallback()
    {
        // Arrange
        var flats = Enumerable.Range(1, 25).Select(i => FakeFlatScraper.CreateFlat(i.ToString())).ToList();
        var service = CreateService(new FakeFlatScraper { Flats = flats });
        var reported = new List<int>();

        // Act
        var result = await service.ExecuteAsync(count =>
        {
            reported.Add(count);
            return Task.CompletedTask;
        });

        // Assert
        reported.Should().Equal(20, 5);
        result.TotalFlats.Should().Be(25);
    }

    [Test]
    public async Task ExecuteAsync_WhenASourceFails_PublishesOnlyTheOthersAndStillRefreshes()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { StartException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] };
        var service = CreateService(failing, working);

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Equal("ImmobilienScout24: Login failed");
        _calls.Should().Equal("begin", "add", "publish OhneMakler", "discard", "refresh");
    }

    [Test]
    public async Task ExecuteAsync_WhenTheRefreshThrows_Propagates()
    {
        // Arrange
        _mockAggregationRepository.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Disk full"));
        var service = CreateService(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Act
        var act = () => service.ExecuteAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Disk full");
    }

    [Test]
    public async Task ExecuteAsync_WhenCanceled_RefreshesTheAggregationsWithAnUncanceledTokenAndRethrows()
    {
        // Arrange: the first source is published, the second one is canceled
        using var cts = new CancellationTokenSource();
        var published = new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] };
        var canceled = new FakeFlatScraper("ImmobilienScout24")
        {
            Flats = [FakeFlatScraper.CreateFlat("2")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };
        var service = CreateService(published, canceled);

        // Act
        var act = () => service.ExecuteAsync(cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _calls.Should().Equal("begin", "add", "publish OhneMakler", "discard", "refresh");
        _mockAggregationRepository.Verify(r => r.RefreshAsync(It.Is<CancellationToken>(ct => !ct.IsCancellationRequested)), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenCanceledAndTheRefreshFails_StillThrowsTheCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _mockAggregationRepository.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Disk full"));
        var scraper = new FakeFlatScraper
        {
            Flats = [FakeFlatScraper.CreateFlat("1")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };
        var service = CreateService(scraper);

        // Act
        var act = () => service.ExecuteAsync(cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Messages.Should().Contain("Failed to refresh flat aggregations after the scrape was canceled");
    }

    private ScrapeExecutionService CreateService(params IScraper[] scrapers)
    {
        var coordinator = new ScraperCoordinator(
            scrapers, _mockFlatRepository.Object, new JsonlBackupWriter(), _dataDir, NullLogger<ScraperCoordinator>.Instance);
        return new ScrapeExecutionService(_mockAggregationRepository.Object, coordinator, _logger);
    }
}
