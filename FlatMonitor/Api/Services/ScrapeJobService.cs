namespace FlatMonitor.Api.Services;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

/// <summary>
/// The scrape job queue, shared by <see cref="Controllers.ScrapeController"/> (queue, cancel, status) and
/// <see cref="ScrapeBackgroundService"/> (run). At most one job is queued or running at a time.
/// </summary>
public class ScrapeJobService
{
    public record ScrapeJob(int Id, Guid JobId, DateTime CreatedAtUtc, ScrapeJobStatus Status = ScrapeJobStatus.Queued);

    public enum ScrapeJobStatus { Queued, Running, Completed, Failed, Canceled }

    /// <summary>Finished jobs kept for <see cref="GetJobStatus"/>; ScrapingHistories keeps the full record.</summary>
    public const int MaxTrackedJobs = 20;

    private readonly Channel<ScrapeJob> _queue = Channel.CreateUnbounded<ScrapeJob>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, ScrapeJob> _jobTracker = new();
    private readonly Lock _lock = new();
    private int _jobCounter;
    private CancellationTokenSource? _currentJobCts;

    /// <summary>Queues a new job, unless one is already queued or running.</summary>
    public bool TryQueueJob([NotNullWhen(true)] out ScrapeJob? job)
    {
        lock (_lock)
        {
            if (IsScrapingInProgress())
            {
                job = null;
                return false;
            }

            job = new ScrapeJob(++_jobCounter, Guid.NewGuid(), DateTime.UtcNow);
            _jobTracker[job.JobId] = job;
            ForgetOldestFinishedJobs();
            _queue.Writer.TryWrite(job);
            return true;
        }
    }

    /// <summary>Waits for the next queued job.</summary>
    public ValueTask<ScrapeJob> DequeueAsync(CancellationToken ct) => _queue.Reader.ReadAsync(ct);

    /// <summary>
    /// Marks the job as running and returns the token that <see cref="CancelCurrentJob"/> cancels,
    /// or <c>null</c> if the job was canceled while it was still queued.
    /// </summary>
    public CancellationToken? StartJob(ScrapeJob job, CancellationToken stoppingToken)
    {
        lock (_lock)
        {
            if (GetJobStatus(job.JobId)?.Status != ScrapeJobStatus.Queued)
                return null;

            _currentJobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            SetStatus(job.JobId, ScrapeJobStatus.Running);
            return _currentJobCts.Token;
        }
    }

    public void FinishJob(Guid jobId, ScrapeJobStatus status)
    {
        lock (_lock)
        {
            SetStatus(jobId, status);
            _currentJobCts?.Dispose();
            _currentJobCts = null;
        }
    }

    /// <summary>Cancels the running job, or a queued job before it starts. Returns false if there is none.</summary>
    public bool CancelCurrentJob()
    {
        lock (_lock)
        {
            if (_currentJobCts != null)
            {
                if (_currentJobCts.IsCancellationRequested)
                    return false;
                _currentJobCts.Cancel();
                return true;
            }

            var queued = _jobTracker.Values.FirstOrDefault(j => j.Status == ScrapeJobStatus.Queued);
            if (queued == null)
                return false;
            SetStatus(queued.JobId, ScrapeJobStatus.Canceled);
            return true;
        }
    }

    public ScrapeJob? GetJobStatus(Guid jobId)
    {
        _jobTracker.TryGetValue(jobId, out var job);
        return job;
    }

    /// <summary>True while a job is queued or running.</summary>
    public bool IsScrapingInProgress() =>
        _jobTracker.Values.Any(job => job.Status is ScrapeJobStatus.Queued or ScrapeJobStatus.Running);

    private void SetStatus(Guid jobId, ScrapeJobStatus status)
    {
        if (_jobTracker.TryGetValue(jobId, out var job))
            _jobTracker[jobId] = job with { Status = status };
    }

    private void ForgetOldestFinishedJobs()
    {
        var finished = _jobTracker.Values
            .Where(j => j.Status is not (ScrapeJobStatus.Queued or ScrapeJobStatus.Running))
            .OrderBy(j => j.Id)
            .ToList();
        foreach (var job in finished.Take(_jobTracker.Count - MaxTrackedJobs))
            _jobTracker.TryRemove(job.JobId, out _);
    }
}
