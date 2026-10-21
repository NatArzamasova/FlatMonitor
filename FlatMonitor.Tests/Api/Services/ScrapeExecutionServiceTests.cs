using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using workInProgress.Api.Services;
using workInProgress.Data;
using workInProgress.Scraping;
using workInProgress.Storage;

namespace workInProgress.Tests.Api.Services;

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
    private FlatsDbContext _dbContext = null!;
    private Mock<ISnapshotRepository> _mockSnapshotRepository = null!;
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private Mock<ILogger<ScrapeExecutionService>> _mockLogger = null!;
    private ScraperCoordinator _coordinator = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase($"ScrapeExecutionServiceTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new FlatsDbContext(options);
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

    [TearDown]
    public async Task TearDown()
    {
        await _coordinator.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public void Constructor_RequiresDbContext()
    {
        var action = () => new ScrapeExecutionService(
            null!,
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresSnapshotRepository()
    {
        var action = () => new ScrapeExecutionService(
            _dbContext,
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
            _dbContext,
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
            _dbContext,
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
            _dbContext,
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            _coordinator,
            null!);

        action.Should().Throw<ArgumentNullException>();
    }
}
