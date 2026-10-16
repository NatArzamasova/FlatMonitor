using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using workInProgress.Api.Services;

namespace workInProgress.Tests;

[TestFixture]
public class ScrapeBackgroundServiceTests
{
    private ScrapeJobService _jobService = null!;
    private ScrapeStateService _stateService = null!;
    private Mock<ILogger<ScrapeBackgroundService>> _mockLogger = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();
        _stateService = new ScrapeStateService();
        _mockLogger = new Mock<ILogger<ScrapeBackgroundService>>();
    }

    [Test]
    public void CanCreateBackgroundServiceWithDependencies()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();

        var backgroundService = new ScrapeBackgroundService(
            mockServiceProvider.Object,
            _jobService,
            _stateService,
            _mockLogger.Object);

        backgroundService.Should().NotBeNull();
    }

    [Test]
    public void CancelCurrentJob_WithNoRunningJob_DoesNotThrow()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var backgroundService = new ScrapeBackgroundService(
            mockServiceProvider.Object,
            _jobService,
            _stateService,
            _mockLogger.Object);

        var action = () => backgroundService.CancelCurrentJob();

        action.Should().NotThrow();
    }

    [Test]
    public void JobService_CanQueueMultipleJobs()
    {
        var job1 = _jobService.QueueJob();
        var job2 = _jobService.QueueJob();
        var job3 = _jobService.QueueJob();

        _jobService.QueuedJobCount.Should().Be(3);
        job1.JobId.Should().NotBe(job2.JobId);
        job2.JobId.Should().NotBe(job3.JobId);
    }

    [Test]
    public void JobService_TracksJobStatusAfterDequeue()
    {
        var job = _jobService.QueueJob();
        var jobId = job.JobId;

        _jobService.TryDequeueJob(out var dequeuedJob);

        var trackedJob = _jobService.GetJobStatus(jobId);
        trackedJob.Should().NotBeNull();
        trackedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Queued);
    }

    [Test]
    public void JobService_CanUpdateJobStatus()
    {
        var job = _jobService.QueueJob();

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);

        var updatedJob = _jobService.GetJobStatus(job.JobId);
        updatedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Running);
    }

    [Test]
    public void JobService_CanTransitionJobToCompleted()
    {
        var job = _jobService.QueueJob();

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);
        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Completed);

        var finalJob = _jobService.GetJobStatus(job.JobId);
        finalJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Completed);
    }

    [Test]
    public void JobService_MaintainsJobIdAcrossStatusUpdates()
    {
        var originalJob = _jobService.QueueJob();
        var originalJobId = originalJob.JobId;

        _jobService.UpdateJobStatus(originalJobId, ScrapeJobService.ScrapeJobStatus.Running);

        var updatedJob = _jobService.GetJobStatus(originalJobId);
        updatedJob!.JobId.Should().Be(originalJobId);
    }

    [Test]
    public void JobService_SupportsAllTransitions()
    {
        var job = _jobService.QueueJob();
        var jobId = job.JobId;

        _jobService.UpdateJobStatus(jobId, ScrapeJobService.ScrapeJobStatus.Running);
        _jobService.UpdateJobStatus(jobId, ScrapeJobService.ScrapeJobStatus.Completed);

        var completedJob = _jobService.GetJobStatus(jobId);
        completedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Completed);

        var job2 = _jobService.QueueJob();
        _jobService.UpdateJobStatus(job2.JobId, ScrapeJobService.ScrapeJobStatus.Failed);
        var failedJob = _jobService.GetJobStatus(job2.JobId);
        failedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Failed);

        var job3 = _jobService.QueueJob();
        _jobService.UpdateJobStatus(job3.JobId, ScrapeJobService.ScrapeJobStatus.Running);
        _jobService.UpdateJobStatus(job3.JobId, ScrapeJobService.ScrapeJobStatus.Canceled);
        var canceledJob = _jobService.GetJobStatus(job3.JobId);
        canceledJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Canceled);
    }
}
