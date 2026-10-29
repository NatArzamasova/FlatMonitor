namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Api.Services;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ControllerBase
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
    public ActionResult<ScrapeResponse> TriggerScrapeAsync()
    {
        if (!_jobService.TryQueueJob(out var job))
        {
            return Conflict(new ScrapeResponse
            {
                Success = false,
                Message = "A scrape is already queued or running",
                ScrapedAtUtc = DateTime.UtcNow,
            });
        }

        return Accepted($"api/scrape/status/{job.JobId}", new ScrapeResponse
        {
            Success = true,
            Message = "Scrape job queued",
            ScrapedAtUtc = job.CreatedAtUtc,
            FlatsCount = 0
        });
    }

    [HttpGet("status")]
    public async Task<ActionResult<ScrapingStatusResponse>> GetScrapingStatus()
    {
        var isScrapingInProgress = _jobService.IsScrapingInProgress();

        var histories = await _historyRepository.GetLatestAsync();

        var response = new ScrapingStatusResponse
        {
            IsScrapingInProgress = isScrapingInProgress,
            LastScrapingHistory = histories.Latest,
            LastSuccessfulScrapingHistory = histories.LatestSuccessful
        };

        return Ok(response);
    }

    [HttpGet("status/{jobId}")]
    public ActionResult<ScrapeJobStatusResponse> GetJobStatus(Guid jobId)
    {
        var job = _jobService.GetJobStatus(jobId);
        if (job == null)
        {
            return NotFound(new { message = $"Job {jobId} not found" });
        }

        return Ok(new ScrapeJobStatusResponse
        {
            JobId = job.JobId,
            Status = job.Status.ToString(),
            CreatedAtUtc = job.CreatedAtUtc
        });
    }

    [HttpPost("cancel")]
    public ActionResult CancelScrape()
    {
        var message = _jobService.CancelCurrentJob()
            ? "Scrape cancellation requested"
            : "No scrape is queued or running";
        return Ok(new { message });
    }
}
