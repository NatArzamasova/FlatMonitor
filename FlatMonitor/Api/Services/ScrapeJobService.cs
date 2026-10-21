namespace workInProgress.Api.Services;

using System.Collections.Concurrent;

public class ScrapeJobService
{
    public record ScrapeJob(int Id, Guid JobId, DateTime CreatedAtUtc, ScrapeJobStatus Status = ScrapeJobStatus.Queued);

    public enum ScrapeJobStatus { Queued, Running, Completed, Failed, Canceled }

    private readonly ConcurrentQueue<ScrapeJob> _jobQueue = new();
    private readonly ConcurrentDictionary<Guid, ScrapeJob> _jobTracker = new();
    private int _jobCounter = 0;

    public ScrapeJob QueueJob()
    {
        var jobId = Guid.NewGuid();
        var job = new ScrapeJob(++_jobCounter, jobId, DateTime.UtcNow, ScrapeJobStatus.Queued);
        _jobQueue.Enqueue(job);
        _jobTracker.TryAdd(jobId, job);
        return job;
    }

    public bool TryDequeueJob(out ScrapeJob? job)
    {
        return _jobQueue.TryDequeue(out job);
    }

    public ScrapeJob? GetJobStatus(Guid jobId)
    {
        _jobTracker.TryGetValue(jobId, out var job);
        return job;
    }

    public void UpdateJobStatus(Guid jobId, ScrapeJobStatus status)
    {
        if (_jobTracker.TryGetValue(jobId, out var job))
        {
            var updatedJob = job with { Status = status };
            _jobTracker[jobId] = updatedJob;
        }
    }

    public int QueuedJobCount => _jobQueue.Count;

    public bool IsScrapingInProgress()
    {
        return _jobTracker.Values.Any(job => job.Status == ScrapeJobStatus.Running);
    }
}
