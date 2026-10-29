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
    private Mock<ISnapshotRepository> _mockSnapshotRepository = null!;
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private Mock<ILogger<ScrapeExecutionService>> _mockLogger = null!;
    private ScraperCoordinator _coordinator = null!;

    [SetUp]
    public void Setup()
    {
        _mockSnapshotRepository = new Mock<ISnapshotRepository>();
        _mockAggregationRepository = new Mock<IFlatAggregationRepository>();
        _mockLogger = new Mock<ILogger<ScrapeExecutionService>>();
        _coordinator = new ScraperCoordinator(
            Array.Empty<IScraper>(),
            _mockSnapshotRepository.Object,
            new SnapshotWriter(),
            Path.GetTempPath(),
            NullLogger<ScraperCoordinator>.Instance);

        _mockSnapshotRepository
            .Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSnapshotRepository
            .Setup(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAggregationRepository
            .Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Test]
    public void Constructor_RequiresSnapshotRepository()
    {
        var action = () => new ScrapeExecutionService(
            null!,
            _mockAggregationRepository.Object,
            _coordinator,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresAggregationRepository()
    {
        var action = () => new ScrapeExecutionService(
            _mockSnapshotRepository.Object,
            null!,
            _coordinator,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresCoordinator()
    {
        var action = () => new ScrapeExecutionService(
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresLogger()
    {
        var action = () => new ScrapeExecutionService(
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            _coordinator,
            null!);

        action.Should().Throw<ArgumentNullException>();
    }
}

[TestFixture]
public class ScrapeExecutionServiceExecuteTests
{
    private Mock<ISnapshotRepository> _mockSnapshotRepository = null!;
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private ListLogger<ScrapeExecutionService> _logger = null!;
    private string _dataDir = null!;

    [SetUp]
    public void Setup()
    {
        // Loose mocks: every repository call completes without doing anything
        _mockSnapshotRepository = new Mock<ISnapshotRepository>();
        _mockAggregationRepository = new Mock<IFlatAggregationRepository>();
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
    public async Task ExecuteAsync_OnSuccess_LogsEachStepInOrder()
    {
        // Arrange
        var service = CreateService(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();
        _logger.Messages.Should().Equal(
            "Backing up FlatsSnapshots table",
            "Clearing FlatsSnapshots table",
            "Refreshing flat aggregations",
            "Scrape successful, deleting backup");
    }

    [Test]
    public async Task ExecuteAsync_BacksUpThenClearsThroughRepositoryBeforeScraping()
    {
        // Arrange
        var calls = new List<string>();
        _mockSnapshotRepository.Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("backup")).Returns(Task.CompletedTask);
        _mockSnapshotRepository.Setup(r => r.ClearAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("clear")).Returns(Task.CompletedTask);
        _mockSnapshotRepository.Setup(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("upsert")).Returns(Task.CompletedTask);
        var service = CreateService(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Act
        await service.ExecuteAsync();

        // Assert
        calls.Should().Equal("backup", "clear", "upsert");
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
    public async Task ExecuteAsync_WhenASourceFails_KeepsBackupAndReportsFailure()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") };
        var service = CreateService(failing);

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Equal("ImmobilienScout24: Login failed");
        _mockSnapshotRepository.Verify(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockSnapshotRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _logger.Messages.Should().NotContain("Scrape successful, deleting backup");
    }

    [Test]
    public async Task ExecuteAsync_WhenAStepThrows_RestoresBackupAndRethrows()
    {
        // Arrange
        _mockAggregationRepository.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Disk full"));
        var service = CreateService(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Act
        var act = () => service.ExecuteAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Disk full");
        _mockSnapshotRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSnapshotRepository.Verify(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenCanceled_RestoresBackupWithAnUncanceledToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
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
        _mockSnapshotRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(
            It.Is<CancellationToken>(ct => !ct.IsCancellationRequested)), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenBackupFails_DoesNotRestoreAnOlderBackup()
    {
        // Arrange
        _mockSnapshotRepository.Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database locked"));
        var service = CreateService();

        // Act
        var act = () => service.ExecuteAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database locked");
        _mockSnapshotRepository.Verify(r => r.ClearAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockSnapshotRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenRestoreAlsoFails_PropagatesTheOriginalError()
    {
        // Arrange
        _mockAggregationRepository.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Disk full"));
        _mockSnapshotRepository.Setup(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No backup table"));
        var service = CreateService();

        // Act
        var act = () => service.ExecuteAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Disk full");
        _logger.Messages.Should().Contain("Failed to restore FlatsSnapshots from backup");
    }

    private ScrapeExecutionService CreateService(params IScraper[] scrapers)
    {
        var coordinator = new ScraperCoordinator(
            scrapers, _mockSnapshotRepository.Object, new SnapshotWriter(), _dataDir, NullLogger<ScraperCoordinator>.Instance);
        return new ScrapeExecutionService(
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            coordinator,
            _logger);
    }
}
