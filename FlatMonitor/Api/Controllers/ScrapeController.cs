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
    private readonly ScrapeBackgroundService _backgroundService;
    private readonly IScrapingHistoryRepository _historyRepository;

    public ScrapeController(ScrapeJobService jobService, ScrapeBackgroundService backgroundService, IScrapingHistoryRepository historyRepository)
    {
        _jobService = jobService;
        _backgroundService = backgroundService;
        _historyRepository = historyRepository;
    }

    [HttpPost]
    public ActionResult<ScrapeResponse> TriggerScrapeAsync()
    {
        var job = _jobService.QueueJob();
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
        _backgroundService.CancelCurrentJob();
        return Ok(new { message = "Scrape cancellation requested" });
    }
}
