namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Api.Services;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ApiControllerBase
{
    private readonly ScrapeJobService _jobService;
    private readonly IScrapingHistoryRepository _historyRepository;

    public ScrapeController(ScrapeJobService jobService, IScrapingHistoryRepository historyRepository)
    {
        _jobService = jobService;
        _historyRepository = historyRepository;
    }

    /// <summary>Queues a scrape; 409 Conflict while another one is queued or running.</summary>
    [HttpPost]
    public ActionResult<ApiResponse<ScrapeQueuedResponse>> TriggerScrape()
    {
        if (!_jobService.TryQueueJob(out var job))
            return Conflict(Failure<ScrapeQueuedResponse>("A scrape is already queued or running"));

        var queued = new ScrapeQueuedResponse { JobId = job.JobId, QueuedAtUtc = job.CreatedAtUtc };
        return Accepted($"api/scrape/status/{job.JobId}", Success(queued, "Scrape job queued"));
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<ScrapingStatusResponse>>> GetScrapingStatus(CancellationToken ct = default)
    {
        var histories = await _historyRepository.GetLatestAsync(ct);

        var status = new ScrapingStatusResponse
        {
            IsScrapingInProgress = _jobService.IsScrapingInProgress(),
            LastScrapingHistory = histories.Latest,
            LastSuccessfulScrapingHistory = histories.LatestSuccessful
        };

        return Ok(Success(status, "Scraping status fetched successfully"));
    }

    [HttpGet("status/{jobId}")]
    public ActionResult<ApiResponse<ScrapeJobStatusResponse>> GetJobStatus(Guid jobId)
    {
        var job = _jobService.GetJobStatus(jobId);
        if (job == null)
            return NotFound(Failure<ScrapeJobStatusResponse>($"Job {jobId} not found"));

        return Ok(Success(new ScrapeJobStatusResponse
        {
            JobId = job.JobId,
            Status = job.Status.ToString(),
            CreatedAtUtc = job.CreatedAtUtc
        }, "Job status fetched successfully"));
    }

    /// <summary>Cancels the queued or running scrape; <c>data</c> says whether there was one.</summary>
    [HttpPost("cancel")]
    public ActionResult<ApiResponse<bool>> CancelScrape()
    {
        var canceled = _jobService.CancelCurrentJob();
        return Ok(Success(canceled, canceled ? "Scrape cancellation requested" : "No scrape is queued or running"));
    }
}
