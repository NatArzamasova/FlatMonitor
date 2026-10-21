namespace workInProgress.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using workInProgress.Api.Models;
using workInProgress.Api.Services;
using workInProgress.Data;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ControllerBase
{
    private readonly ScrapeJobService _jobService;
    private readonly ScrapeBackgroundService _backgroundService;
    private readonly FlatsDbContext _context;

    public ScrapeController(ScrapeJobService jobService, ScrapeBackgroundService backgroundService, FlatsDbContext context)
    {
        _jobService = jobService;
        _backgroundService = backgroundService;
        _context = context;
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

        var lastScrapingHistory = await _context.ScrapingHistories
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync();

        var lastSuccessfulScrapingHistory = await _context.ScrapingHistories
            .Where(h => h.Status == "Successful")
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync();

        var response = new ScrapingStatusResponse
        {
            IsScrapingInProgress = isScrapingInProgress,
            LastScrapingHistory = lastScrapingHistory != null ? new ScrapingHistoryDto
            {
                Id = lastScrapingHistory.Id,
                StartTimeUtc = lastScrapingHistory.StartTimeUtc,
                EndTimeUtc = lastScrapingHistory.EndTimeUtc,
                Status = lastScrapingHistory.Status,
                ErrorMessage = lastScrapingHistory.ErrorMessage,
                FlatsScrapedCount = lastScrapingHistory.FlatsScrapedCount
            } : null,
            LastSuccessfulScrapingHistory = lastSuccessfulScrapingHistory != null ? new ScrapingHistoryDto
            {
                Id = lastSuccessfulScrapingHistory.Id,
                StartTimeUtc = lastSuccessfulScrapingHistory.StartTimeUtc,
                EndTimeUtc = lastSuccessfulScrapingHistory.EndTimeUtc,
                Status = lastSuccessfulScrapingHistory.Status,
                ErrorMessage = lastSuccessfulScrapingHistory.ErrorMessage,
                FlatsScrapedCount = lastSuccessfulScrapingHistory.FlatsScrapedCount
            } : null
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
