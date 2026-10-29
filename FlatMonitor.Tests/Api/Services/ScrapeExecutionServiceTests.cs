using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;

namespace FlatMonitor.Tests.Api.Services;

[TestFixture]
public class ScrapeResultTests
{
    [Test]
    public void ScrapeResult_InitializesWithDefaults()
    {
        var result = new ScrapeResult();

        result.Success.Should().BeFalse();
        result.FlatsCount.Should().Be(0);
        result.ScraperErrors.Should().BeEmpty();
    }

    [Test]
    public void ScrapeResult_CanSetProperties()
    {
        var result = new ScrapeResult
        {
            Success = true,
            FlatsCount = 42,
            ScraperErrors = new List<string> { "Error 1" }
        };

        result.Success.Should().BeTrue();
        result.FlatsCount.Should().Be(42);
        result.ScraperErrors.Should().HaveCount(1);
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
            Path.GetTempPath());

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
    public async Task ExecuteAsync_WhenASourceFails_KeepsBackupAndReportsFailure()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") };
        var service = CreateService(failing);

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ScraperErrors.Should().Equal("ImmobilienScout24: Login failed");
        _mockSnapshotRepository.Verify(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()), Times.Never);
        _logger.Messages.Should().NotContain("Scrape successful, deleting backup");
    }

    private ScrapeExecutionService CreateService(params IScraper[] scrapers)
    {
        var coordinator = new ScraperCoordinator(scrapers, _mockSnapshotRepository.Object, new SnapshotWriter(), _dataDir);
        return new ScrapeExecutionService(
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            coordinator,
            _logger);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
