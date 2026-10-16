namespace workInProgress.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using workInProgress.Api.Models;
using workInProgress.Api.Services;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Scraping;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ControllerBase
{
    private readonly ScraperCoordinator _coordinator;
    private readonly ScrapeStateService _stateService;
    private readonly FlatsDbContext _context;

    public ScrapeController(ScraperCoordinator coordinator, ScrapeStateService stateService, FlatsDbContext context)
    {
        _coordinator = coordinator;
        _stateService = stateService;
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<ScrapeResponse>> TriggerScrapeAsync()
    {
        if (_stateService.IsScrapingInProgress)
        {
            return BadRequest(new ScrapeResponse
            {
                Success = false,
                Message = "Scraping is already in progress",
                ScrapedAtUtc = DateTime.UtcNow,
                FlatsCount = 0
            });
        }

        var startTime = DateTime.UtcNow;
        var history = new ScrapingHistory
        {
            StartTimeUtc = startTime,
            Status = "Running"
        };

        try
        {
            // Save history entry
            _context.ScrapingHistories.Add(history);
            await _context.SaveChangesAsync();

            // Clear all existing snapshots before scraping
            await _context.FlatsSnapshots.ExecuteDeleteAsync();
            await _context.SaveChangesAsync();

            // Get cancellation token
            var cancellationToken = _stateService.StartScraping();

            // Run scraper with cancellation token
            await _coordinator.ScrapeAllSourcesAsync(cancellationToken);

            // Update history on success
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Successful";
            _context.ScrapingHistories.Update(history);
            await _context.SaveChangesAsync();

            return Ok(new ScrapeResponse
            {
                Success = true,
                Message = "Scrape completed successfully",
                ScrapedAtUtc = endTime,
                FlatsCount = 0
            });
        }
        catch (OperationCanceledException)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Canceled";
            _context.ScrapingHistories.Update(history);
            await _context.SaveChangesAsync();

            return Ok(new ScrapeResponse
            {
                Success = true,
                Message = "Scrape was canceled",
                ScrapedAtUtc = endTime,
                FlatsCount = 0
            });
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Failed";
            history.ErrorMessage = ex.Message;
            _context.ScrapingHistories.Update(history);
            await _context.SaveChangesAsync();

            return StatusCode(500, new ScrapeResponse
            {
                Success = false,
                Message = $"Scrape failed: {ex.Message}",
                ScrapedAtUtc = endTime,
                FlatsCount = 0
            });
        }
        finally
        {
            _stateService.FinishScraping();
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<ScrapingStatusResponse>> GetStatusAsync()
    {
        var lastHistory = await _context.ScrapingHistories
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync();

        var response = new ScrapingStatusResponse
        {
            IsScrapingInProgress = _stateService.IsScrapingInProgress,
            LastScrapingHistory = lastHistory != null ? new ScrapingHistoryDto
            {
                Id = lastHistory.Id,
                StartTimeUtc = lastHistory.StartTimeUtc,
                EndTimeUtc = lastHistory.EndTimeUtc,
                Status = lastHistory.Status,
                ErrorMessage = lastHistory.ErrorMessage
            } : null
        };

        return Ok(response);
    }

    [HttpPost("cancel")]
    public ActionResult CancelScrape()
    {
        if (!_stateService.IsScrapingInProgress)
        {
            return BadRequest(new { message = "No scraping in progress" });
        }

        _stateService.StopScraping();
        return Ok(new { message = "Scrape cancellation requested" });
    }
}
