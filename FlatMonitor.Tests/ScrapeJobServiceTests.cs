using NUnit.Framework;
using FluentAssertions;
using workInProgress.Api.Services;

namespace workInProgress.Tests;

[TestFixture]
public class ScrapeJobServiceTests
{
    private ScrapeJobService _jobService = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();
    }

    [Test]
    public void QueueJob_ReturnsJobWithStatusQueued()
    {
        var job = _jobService.QueueJob();

        job.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Queued);
        job.JobId.Should().NotBeEmpty();
        job.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void QueueJob_AssignsSequentialIds()
    {
        var job1 = _jobService.QueueJob();
        var job2 = _jobService.QueueJob();
        var job3 = _jobService.QueueJob();

        job1.Id.Should().Be(1);
        job2.Id.Should().Be(2);
        job3.Id.Should().Be(3);
    }

    [Test]
    public void QueueJob_EachJobHasUniqueGuid()
    {
        var job1 = _jobService.QueueJob();
        var job2 = _jobService.QueueJob();

        job1.JobId.Should().NotBe(job2.JobId);
    }

    [Test]
    public void TryDequeueJob_ReturnsFirstQueuedJob()
    {
        var job1 = _jobService.QueueJob();
        var job2 = _jobService.QueueJob();

        var success = _jobService.TryDequeueJob(out var dequeuedJob);

        success.Should().BeTrue();
        dequeuedJob.Should().NotBeNull();
        dequeuedJob!.JobId.Should().Be(job1.JobId);
    }

    [Test]
    public void TryDequeueJob_WhenEmpty_ReturnsFalse()
    {
        var success = _jobService.TryDequeueJob(out var job);

        success.Should().BeFalse();
        job.Should().BeNull();
    }

    [Test]
    public void TryDequeueJob_RemovesJobFromQueue()
    {
        _jobService.QueueJob();
        _jobService.TryDequeueJob(out _);

        _jobService.QueuedJobCount.Should().Be(0);
    }

    [Test]
    public void TryDequeueJob_MultipleJobs_DequeuesInOrder()
    {
        var job1 = _jobService.QueueJob();
        var job2 = _jobService.QueueJob();
        var job3 = _jobService.QueueJob();

        _jobService.TryDequeueJob(out var dequeued1);
        _jobService.TryDequeueJob(out var dequeued2);
        _jobService.TryDequeueJob(out var dequeued3);

        dequeued1!.JobId.Should().Be(job1.JobId);
        dequeued2!.JobId.Should().Be(job2.JobId);
        dequeued3!.JobId.Should().Be(job3.JobId);
    }

    [Test]
    public void GetJobStatus_ReturnsJobByGuid()
    {
        var job = _jobService.QueueJob();

        var retrievedJob = _jobService.GetJobStatus(job.JobId);

        retrievedJob.Should().NotBeNull();
        retrievedJob!.JobId.Should().Be(job.JobId);
    }

    [Test]
    public void GetJobStatus_NonExistentJob_ReturnsNull()
    {
        var retrievedJob = _jobService.GetJobStatus(Guid.NewGuid());

        retrievedJob.Should().BeNull();
    }

    [Test]
    public void UpdateJobStatus_ChangesJobStatus()
    {
        var job = _jobService.QueueJob();

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);
        var updatedJob = _jobService.GetJobStatus(job.JobId);

        updatedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Running);
    }

    [Test]
    public void UpdateJobStatus_CanUpdateToMultipleStatuses()
    {
        var job = _jobService.QueueJob();

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);
        var runningJob = _jobService.GetJobStatus(job.JobId);
        runningJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Running);

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Completed);
        var completedJob = _jobService.GetJobStatus(job.JobId);
        completedJob!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Completed);
    }

    [Test]
    public void UpdateJobStatus_NonExistentJob_DoesNotThrow()
    {
        var nonExistentJobId = Guid.NewGuid();

        var action = () => _jobService.UpdateJobStatus(nonExistentJobId, ScrapeJobService.ScrapeJobStatus.Running);

        action.Should().NotThrow();
    }

    [Test]
    public void QueuedJobCount_ReflectsQueueSize()
    {
        _jobService.QueuedJobCount.Should().Be(0);

        _jobService.QueueJob();
        _jobService.QueuedJobCount.Should().Be(1);

        _jobService.QueueJob();
        _jobService.QueuedJobCount.Should().Be(2);

        _jobService.TryDequeueJob(out _);
        _jobService.QueuedJobCount.Should().Be(1);
    }

    [Test]
    public void QueueJob_JobStaysInTrackerAfterDequeue()
    {
        var job = _jobService.QueueJob();
        var jobId = job.JobId;

        _jobService.TryDequeueJob(out _);

        var trackedJob = _jobService.GetJobStatus(jobId);
        trackedJob.Should().NotBeNull();
        trackedJob!.JobId.Should().Be(jobId);
    }
}
