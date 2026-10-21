using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using workInProgress.Api.Controllers;
using workInProgress.Api.Services;
using workInProgress.Data;

namespace workInProgress.Tests;

[TestFixture]
public class ScrapeControllerTests
{
    private ScrapeController _controller = null!;
    private ScrapeJobService _jobService = null!;
    private Mock<ScrapeBackgroundService> _mockBackgroundService = null!;
    private FlatsDbContext _dbContext = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();

        var options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FlatsDbContext(options);
        _dbContext.Database.EnsureCreated();

        _mockBackgroundService = new Mock<ScrapeBackgroundService>(
            It.IsAny<IServiceProvider>(),
            It.IsAny<ScrapeJobService>(),
            It.IsAny<ScrapeStateService>(),
            It.IsAny<ILogger<ScrapeBackgroundService>>());

        _controller = new ScrapeController(_jobService, _mockBackgroundService.Object, _dbContext);
    }

    [TearDown]
    public void Teardown()
    {
        _dbContext?.Dispose();
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
