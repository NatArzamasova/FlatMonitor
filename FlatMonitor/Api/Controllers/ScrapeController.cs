namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ApiControllerBase
{
    private readonly ScrapeJobService _jobService;
    private readonly IScrapingHistoryRepository _historyRepository;
    private readonly AvailableScraperSources _sources;

    public ScrapeController(
        ScrapeJobService jobService, IScrapingHistoryRepository historyRepository, AvailableScraperSources sources)
    {
        _jobService = jobService;
        _historyRepository = historyRepository;
        _sources = sources;
    }

    /// <summary>The sources a scrape can run, in the order they are reported.</summary>
    [HttpGet("sources")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetSources() =>
        Ok(Success(_sources.Names, "Scrape sources fetched successfully"));

    /// <summary>
    /// Queues a scrape of the <paramref name="request"/>'s sources (no body: all); the sources left out keep their
    /// flats. 400 Bad Request for no or an unknown source, 409 Conflict while another scrape is queued or running.
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<ScrapeQueuedResponse>> TriggerScrape(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ScrapeRequest? request = null)
    {
        IReadOnlyList<string>? sources;
        try
        {
            // All available ones stay null: the job then runs whatever the run service has
            sources = request?.Sources == null ? null : _sources.Select(request.Sources);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Failure<ScrapeQueuedResponse>(ex.Message));
        }

        if (!_jobService.TryQueueJob(sources, out var job))
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
            LastScrapingHistory = ScrapingHistoryDto.From(histories.Latest),
            LastSuccessfulScrapingHistory = ScrapingHistoryDto.From(histories.LatestSuccessful)
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
            Status = job.Status,
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
