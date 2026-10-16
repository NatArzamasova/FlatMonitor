namespace workInProgress.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using workInProgress.Api.Models;
using workInProgress.Api.Services;
using workInProgress.Data;
using workInProgress.Models;

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
    public ActionResult<ScrapeQueuedResponse> TriggerScrapeAsync()
    {
        var job = _jobService.QueueJob();
        return Accepted($"api/scrape/status/{job.JobId}", new ScrapeQueuedResponse
        {
            JobId = job.JobId,
            Message = "Scrape job queued",
            QueuedAtUtc = job.CreatedAtUtc
        });
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

    [HttpPost("cancel/{jobId}")]
    public ActionResult CancelScrape(Guid jobId)
    {
        var job = _jobService.GetJobStatus(jobId);
        if (job == null)
        {
            return NotFound(new { message = $"Job {jobId} not found" });
        }

        if (job.Status == ScrapeJobService.ScrapeJobStatus.Running)
        {
            _backgroundService.CancelCurrentJob();
            return Ok(new { message = "Scrape cancellation requested" });
        }

        return BadRequest(new { message = $"Cannot cancel job with status {job.Status}" });
    }
}
