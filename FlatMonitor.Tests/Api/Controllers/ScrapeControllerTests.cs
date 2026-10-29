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

    private static ApiResponse<T> Body<T>(ActionResult<ApiResponse<T>> result, Type resultType)
    {
        result.Result.Should().BeOfType(resultType);
        return ((ObjectResult)result.Result!).Value.Should().BeOfType<ApiResponse<T>>().Subject;
    }

    [Test]
    public void TriggerScrape_QueuesJobAndReturnsAcceptedWithTheJob()
    {
        var result = _controller.TriggerScrape();

        var response = Body(result, typeof(AcceptedResult));
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Scrape job queued");
        var job = _jobService.GetJobStatus(response.Data!.JobId);
        job.Should().NotBeNull();
        response.Data.QueuedAtUtc.Should().Be(job!.CreatedAtUtc);
        result.Result.As<AcceptedResult>().Location.Should().Be($"api/scrape/status/{job.JobId}");
    }

    [Test]
    public void TriggerScrape_WhileAJobIsQueuedOrRunning_ReturnsConflictWithoutQueueing()
    {
        _controller.TriggerScrape();

        var result = _controller.TriggerScrape();

        var response = Body(result, typeof(ConflictObjectResult));
        response.Success.Should().BeFalse();
        response.Message.Should().Be("A scrape is already queued or running");
        response.Data.Should().BeNull();
    }

    [Test]
    public void GetJobStatus_WithValidJobId_ReturnsJob()
    {
        _jobService.TryQueueJob(out var job);

        var result = _controller.GetJobStatus(job!.JobId);

        var response = Body(result, typeof(OkObjectResult));
        response.Success.Should().BeTrue();
        response.Data!.Status.Should().Be("Queued");
    }

    [Test]
    public void GetJobStatus_WithInvalidJobId_ReturnsNotFound()
    {
        var jobId = Guid.NewGuid();

        var result = _controller.GetJobStatus(jobId);

        var response = Body(result, typeof(NotFoundObjectResult));
        response.Success.Should().BeFalse();
        response.Message.Should().Be($"Job {jobId} not found");
    }

    [Test]
    public void CancelScrape_WithQueuedJob_CancelsIt()
    {
        _jobService.TryQueueJob(out var job);

        var result = _controller.CancelScrape();

        var response = Body(result, typeof(OkObjectResult));
        response.Data.Should().BeTrue();
        response.Message.Should().Be("Scrape cancellation requested");
        _jobService.GetJobStatus(job!.JobId)!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Canceled);
    }

    [Test]
    public void CancelScrape_WithoutJob_ReturnsOkWithFalse()
    {
        var result = _controller.CancelScrape();

        var response = Body(result, typeof(OkObjectResult));
        response.Success.Should().BeTrue();
        response.Data.Should().BeFalse();
        response.Message.Should().Be("No scrape is queued or running");
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
        using var cts = new CancellationTokenSource();
        _jobService.TryQueueJob(out _);
        _mockHistoryRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScrapingStatusHistory(latest, latestSuccessful));

        var result = await _controller.GetScrapingStatus(cts.Token);

        var response = Body(result, typeof(OkObjectResult));
        response.Success.Should().BeTrue();
        response.Data!.IsScrapingInProgress.Should().BeTrue();
        response.Data.LastScrapingHistory.Should().BeSameAs(latest);
        response.Data.LastSuccessfulScrapingHistory.Should().BeSameAs(latestSuccessful);
        _mockHistoryRepository.Verify(r => r.GetLatestAsync(cts.Token), Times.Once);
    }
}
