using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using workInProgress.Api.Services;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Scraping;
using workInProgress.Storage;

namespace workInProgress.Tests;

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
    private Mock<FlatsDbContext> _mockDbContext = null!;
    private Mock<ISnapshotRepository> _mockSnapshotRepository = null!;
    private Mock<IFlatAggregationRepository> _mockAggregationRepository = null!;
    private Mock<ILogger<ScrapeExecutionService>> _mockLogger = null!;

    [SetUp]
    public void Setup()
    {
        _mockDbContext = new Mock<FlatsDbContext>();
        _mockSnapshotRepository = new Mock<ISnapshotRepository>();
        _mockAggregationRepository = new Mock<IFlatAggregationRepository>();
        _mockLogger = new Mock<ILogger<ScrapeExecutionService>>();

        _mockSnapshotRepository
            .Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSnapshotRepository
            .Setup(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAggregationRepository
            .Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockDbSet = new Mock<DbSet<FlatSnapshot>>();
        _mockDbContext
            .Setup(c => c.FlatsSnapshots)
            .Returns(mockDbSet.Object);

        mockDbSet
            .Setup(s => s.ExecuteDeleteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockDbContext
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
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
            _mockDbContext.Object,
            null!,
            _mockAggregationRepository.Object,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresAggregationRepository()
    {
        var action = () => new ScrapeExecutionService(
            _mockDbContext.Object,
            _mockSnapshotRepository.Object,
            null!,
            null!,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_RequiresCoordinator()
    {
        var action = () => new ScrapeExecutionService(
            _mockDbContext.Object,
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
            _mockDbContext.Object,
            _mockSnapshotRepository.Object,
            _mockAggregationRepository.Object,
            null!,
            null!);

        action.Should().Throw<ArgumentNullException>();
    }
}
