namespace workInProgress.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using workInProgress.Api.Models;
using workInProgress.Data;

[ApiController]
[Route("api/[controller]")]
public class FlatsController : ControllerBase
{
    private readonly FlatsDbContext _context;

    public FlatsController(FlatsDbContext context)
    {
        _context = context;
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<FlatHistoryDto>>>> GetHistoryAsync()
    {
        try
        {
            var history = await _context.FlatAggregations
                .OrderBy(a => a.ScrapingDate)
                .ThenBy(a => a.District)
                .ThenBy(a => a.RoomsCount)
                .Select(a => new FlatHistoryDto
                {
                    ScrapingDate = a.ScrapingDate.ToString("yyyy-MM-dd"),
                    District = a.District,
                    RoomsCount = a.RoomsCount,
                    AvgPricePerMeter = a.AvgPricePerMeter,
                    Count = a.Count
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<FlatHistoryDto>>
            {
                Success = true,
                Data = history,
                Message = "History fetched successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<List<FlatHistoryDto>>
            {
                Success = false,
                Message = $"Error fetching history: {ex.Message}"
            });
        }
    }

    [HttpGet("current-offers")]
    public async Task<ActionResult<ApiResponse<List<FlatOfferDto>>>> GetCurrentOffersAsync()
    {
        try
        {
            var offers = await _context.FlatsSnapshots
                .OrderByDescending(s => s.ScrapedAtUtc)
                .Select(s => new FlatOfferDto
                {
                    District = s.District,
                    RoomsNumber = s.RoomsCount,
                    Price = s.PriceEur,
                    PricePerMeter = s.PricePerMeter,
                    Url = s.Url,
                    Source = s.Source
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<FlatOfferDto>>
            {
                Success = true,
                Data = offers,
                Message = "Current offers fetched successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<List<FlatOfferDto>>
            {
                Success = false,
                Message = $"Error fetching offers: {ex.Message}"
            });
        }
    }

    [HttpGet("aggregated")]
    public async Task<ActionResult<ApiResponse<List<FlatAggregation>>>> GetAggregatedAsync()
    {
        try
        {
            // Get the latest scraping date
            var latestScrapingDate = await _context.FlatAggregations
                .MaxAsync(a => (DateOnly?)a.ScrapingDate);

            // If no data, return empty list
            if (latestScrapingDate == null)
            {
                return Ok(new ApiResponse<List<FlatAggregation>>
                {
                    Success = true,
                    Data = new List<FlatAggregation>(),
                    Message = "Aggregated successfully"
                });
            }

            var aggregated = await _context.FlatAggregations
                .Where(a => a.ScrapingDate == latestScrapingDate)
                .OrderBy(a => a.District)
                .ThenBy(a => a.RoomsCount)
                .Select(a => new FlatAggregation
                {
                    PostalCode = null,
                    District = a.District,
                    RoomsCount = a.RoomsCount,
                    Availability = null,
                    Count = a.Count,
                    AvgPricePerMeter = a.AvgPricePerMeter
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<FlatAggregation>>
            {
                Success = true,
                Data = aggregated,
                Message = "Aggregated successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<List<FlatAggregation>>
            {
                Success = false,
                Message = $"Error aggregating data: {ex.Message}"
            });
        }
    }
}
