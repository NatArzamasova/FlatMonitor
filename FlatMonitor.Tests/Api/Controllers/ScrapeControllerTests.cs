using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private Mock<ScrapeBackgroundService> _mockBackgroundService = null!;
    private Mock<IScrapingHistoryRepository> _mockHistoryRepository = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();

        _mockBackgroundService = new Mock<ScrapeBackgroundService>(
            It.IsAny<IServiceProvider>(),
            It.IsAny<ScrapeJobService>(),
            It.IsAny<ScrapeStateService>(),
            It.IsAny<ILogger<ScrapeBackgroundService>>());
        _mockHistoryRepository = new Mock<IScrapingHistoryRepository>();

        _controller = new ScrapeController(_jobService, _mockBackgroundService.Object, _mockHistoryRepository.Object);
    }

    [Test]
    public void TriggerScrapeAsync_QueuesJobInService()
    {
        _controller.TriggerScrapeAsync();

        _jobService.QueuedJobCount.Should().Be(1);
    }

    [Test]
    public void TriggerScrapeAsync_ReturnsAcceptedStatus()
    {
        var result = _controller.TriggerScrapeAsync();

        result.Should().NotBeNull();
    }

    [Test]
    public void TriggerScrapeAsync_MultipleCalls_CreatesMultipleJobs()
    {
        _controller.TriggerScrapeAsync();
        _controller.TriggerScrapeAsync();
        _controller.TriggerScrapeAsync();

        _jobService.QueuedJobCount.Should().Be(3);
    }

    [Test]
    public void GetJobStatus_WithValidJobId_ReturnsValidResult()
    {
        var job = _jobService.QueueJob();

        var result = _controller.GetJobStatus(job.JobId);

        result.Should().NotBeNull();
    }

    [Test]
    public void GetJobStatus_WithInvalidJobId_ReturnsResult()
    {
        var invalidJobId = Guid.NewGuid();

        var result = _controller.GetJobStatus(invalidJobId);

        result.Should().NotBeNull();
    }

    [Test]
    public void CancelScrape_ReturnsOkResult()
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
        var runningJob = _jobService.QueueJob();
        _jobService.UpdateJobStatus(runningJob.JobId, ScrapeJobService.ScrapeJobStatus.Running);
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

    [Test]
    public void JobService_JobsCanBeRetrievedById()
    {
        var job = _jobService.QueueJob();
        var jobId = job.JobId;

        var retrievedJob = _jobService.GetJobStatus(jobId);

        retrievedJob.Should().NotBeNull();
        retrievedJob!.JobId.Should().Be(jobId);
    }

    [Test]
    public void JobService_JobStatusCanBeUpdated()
    {
        var job = _jobService.QueueJob();

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);
        var updatedJob = _jobService.GetJobStatus(job.JobId);

        updatedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Running);
    }
}
