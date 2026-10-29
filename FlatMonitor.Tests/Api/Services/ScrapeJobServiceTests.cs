using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Api.Services;
using static FlatMonitor.Api.Services.ScrapeJobService;

namespace FlatMonitor.Tests.Api.Services;

[TestFixture]
public class ScrapeJobServiceTests
{
    private ScrapeJobService _jobService = null!;

    [SetUp]
    public void Setup()
    {
        _jobService = new ScrapeJobService();
    }

    private ScrapeJob QueueJob()
    {
        _jobService.TryQueueJob(out var job).Should().BeTrue();
        return job!;
    }

    /// <summary>Queues, dequeues, starts and finishes one job, as the background service does.</summary>
    private async Task<ScrapeJob> RunJobAsync(ScrapeJobStatus finalStatus = ScrapeJobStatus.Completed)
    {
        QueueJob();
        var job = await _jobService.DequeueAsync(CancellationToken.None);
        _jobService.StartJob(job, CancellationToken.None);
        _jobService.FinishJob(job.JobId, finalStatus);
        return job;
    }

    [Test]
    public void TryQueueJob_ReturnsJobWithStatusQueued()
    {
        var job = QueueJob();

        job.Status.Should().Be(ScrapeJobStatus.Queued);
        job.JobId.Should().NotBeEmpty();
        job.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _jobService.IsScrapingInProgress().Should().BeTrue();
    }

    [Test]
    public void TryQueueJob_WhileAJobIsQueued_IsRefused()
    {
        QueueJob();

        _jobService.TryQueueJob(out var second).Should().BeFalse();
        second.Should().BeNull();
    }

    [Test]
    public async Task TryQueueJob_WhileAJobIsRunning_IsRefused()
    {
        QueueJob();
        var job = await _jobService.DequeueAsync(CancellationToken.None);
        _jobService.StartJob(job, CancellationToken.None);

        _jobService.TryQueueJob(out _).Should().BeFalse();
    }

    [Test]
    public async Task TryQueueJob_AfterTheJobFinished_AssignsTheNextIdAndAUniqueGuid()
    {
        var first = await RunJobAsync();

        var second = QueueJob();

        second.Id.Should().Be(first.Id + 1);
        second.JobId.Should().NotBe(first.JobId);
    }

    [Test]
    public async Task DequeueAsync_ReturnsTheQueuedJob()
    {
        var queued = QueueJob();

        var dequeued = await _jobService.DequeueAsync(CancellationToken.None);

        dequeued.JobId.Should().Be(queued.JobId);
    }

    [Test]
    public async Task DequeueAsync_WaitsUntilAJobIsQueued()
    {
        var dequeue = _jobService.DequeueAsync(CancellationToken.None).AsTask();
        dequeue.IsCompleted.Should().BeFalse();

        var queued = QueueJob();

        (await dequeue.WaitAsync(TimeSpan.FromSeconds(5))).JobId.Should().Be(queued.JobId);
    }

    [Test]
    public async Task DequeueAsync_WhenStopping_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var dequeue = _jobService.DequeueAsync(cts.Token).AsTask();

        cts.Cancel();

        await dequeue.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task StartJob_MarksJobRunning()
    {
        var job = QueueJob();
        await _jobService.DequeueAsync(CancellationToken.None);

        var token = _jobService.StartJob(job, CancellationToken.None);

        token.Should().NotBeNull();
        _jobService.GetJobStatus(job.JobId)!.Status.Should().Be(ScrapeJobStatus.Running);
    }

    [Test]
    public async Task FinishJob_SetsFinalStatus_AndIsNoLongerInProgress()
    {
        var job = await RunJobAsync(ScrapeJobStatus.Failed);

        _jobService.GetJobStatus(job.JobId)!.Status.Should().Be(ScrapeJobStatus.Failed);
        _jobService.IsScrapingInProgress().Should().BeFalse();
    }

    [Test]
    public async Task CancelCurrentJob_CancelsTheRunningJobsToken()
    {
        var job = QueueJob();
        await _jobService.DequeueAsync(CancellationToken.None);
        var token = _jobService.StartJob(job, CancellationToken.None)!.Value;

        _jobService.CancelCurrentJob().Should().BeTrue();

        token.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task CancelCurrentJob_BeforeTheJobStarted_MarksItCanceledAndStartJobSkipsIt()
    {
        var job = QueueJob();

        _jobService.CancelCurrentJob().Should().BeTrue();

        _jobService.GetJobStatus(job.JobId)!.Status.Should().Be(ScrapeJobStatus.Canceled);
        _jobService.IsScrapingInProgress().Should().BeFalse();
        var dequeued = await _jobService.DequeueAsync(CancellationToken.None);
        _jobService.StartJob(dequeued, CancellationToken.None).Should().BeNull();
    }

    [Test]
    public void CancelCurrentJob_WithoutJob_ReturnsFalse()
    {
        _jobService.CancelCurrentJob().Should().BeFalse();
    }

    [Test]
    public async Task StoppingTheHost_CancelsTheRunningJob()
    {
        using var stopping = new CancellationTokenSource();
        var job = QueueJob();
        await _jobService.DequeueAsync(CancellationToken.None);
        var token = _jobService.StartJob(job, stopping.Token)!.Value;

        stopping.Cancel();

        token.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public void GetJobStatus_NonExistentJob_ReturnsNull()
    {
        _jobService.GetJobStatus(Guid.NewGuid()).Should().BeNull();
    }

    [Test]
    public async Task FinishedJobs_KeepsOnlyTheMostRecent()
    {
        var jobs = new List<ScrapeJob>();
        for (var i = 0; i < MaxTrackedJobs + 5; i++)
            jobs.Add(await RunJobAsync());

        var current = QueueJob();

        _jobService.GetJobStatus(jobs[0].JobId).Should().BeNull();
        _jobService.GetJobStatus(jobs[^1].JobId).Should().NotBeNull();
        _jobService.GetJobStatus(current.JobId).Should().NotBeNull();
        jobs.Count(j => _jobService.GetJobStatus(j.JobId) != null).Should().Be(MaxTrackedJobs - 1);
    }
}
