using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using FlatMonitor.Api.Services;
using FlatMonitor.Api.Controllers;
using FlatMonitor.Storage;
using FlatMonitor.Api.Contracts;
using FlatMonitor.Models;

namespace FlatMonitor.Tests.Api.Controllers;

[TestFixture]
public class ScrapeControllerTests
{
    private ScrapeController _controller = null!;
    private ScrapeJobService _jobService = null!;
    private Mock<IScrapingHistoryRepository> _mockHistoryRepository = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();
        _mockHistoryRepository = new Mock<IScrapingHistoryRepository>();

        _controller = new ScrapeController(_jobService, _mockHistoryRepository.Object);
    }

    [Test]
    public void TriggerScrapeAsync_QueuesJobAndReturnsAccepted()
    {
        var result = _controller.TriggerScrapeAsync();

        var accepted = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Value.Should().BeOfType<ScrapeResponse>().Which.Success.Should().BeTrue();
        _jobService.IsScrapingInProgress().Should().BeTrue();
    }

    [Test]
    public void TriggerScrapeAsync_WhileAJobIsQueuedOrRunning_ReturnsConflictWithoutQueueing()
    {
        _controller.TriggerScrapeAsync();

        var result = _controller.TriggerScrapeAsync();

        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var response = conflict.Value.Should().BeOfType<ScrapeResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("A scrape is already queued or running");
    }

    [Test]
    public void GetJobStatus_WithValidJobId_ReturnsJob()
    {
        _jobService.TryQueueJob(out var job);

        var result = _controller.GetJobStatus(job!.JobId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ScrapeJobStatusResponse>().Which.Status.Should().Be("Queued");
    }

    [Test]
    public void GetJobStatus_WithInvalidJobId_ReturnsNotFound()
    {
        var result = _controller.GetJobStatus(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public void CancelScrape_WithQueuedJob_CancelsIt()
    {
        _jobService.TryQueueJob(out var job);

        var result = _controller.CancelScrape();

        result.Should().BeOfType<OkObjectResult>();
        _jobService.GetJobStatus(job!.JobId)!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Canceled);
    }

    [Test]
    public void CancelScrape_WithoutJob_ReturnsOk()
    {
        var result = _controller.CancelScrape();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetScrapingStatus_ReturnsHistoryFromRepositoryAndCurrentJobState()
    {
        var latest = new ScrapingHistory
        {
            Id = 2,
            StartTimeUtc = DateTime.UtcNow,
            Status = "Failed",
            FlatsScrapedCount = 1
        };
        var latestSuccessful = new ScrapingHistory
        {
            Id = 1,
            StartTimeUtc = DateTime.UtcNow,
            Status = "Successful",
            FlatsScrapedCount = 5
        };
        _jobService.TryQueueJob(out _);
        _mockHistoryRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScrapingStatusHistory(latest, latestSuccessful));

        var action = await _controller.GetScrapingStatus();
        var response = action.Value ?? (action.Result as OkObjectResult)?.Value as ScrapingStatusResponse;

        response.Should().NotBeNull();
        response!.IsScrapingInProgress.Should().BeTrue();
        response.LastScrapingHistory.Should().BeSameAs(latest);
        response.LastSuccessfulScrapingHistory.Should().BeSameAs(latestSuccessful);
        _mockHistoryRepository.Verify(r => r.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
