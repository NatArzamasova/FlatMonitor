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

    [HttpGet("aggregated")]
    public async Task<ActionResult<ApiResponse<List<FlatAggregation>>>> GetAggregatedAsync()
    {
        try
        {
            var flats = await _context.FlatsSnapshots
                .Where(f => f.District != null)
                .ToListAsync();

            var aggregated = flats
                .GroupBy(f => new { f.District, f.RoomsCount })
                .Select(g => new FlatAggregation
                {
                    PostalCode = null,
                    District = g.Key.District,
                    RoomsCount = g.Key.RoomsCount,
                    Availability = null,
                    Count = g.Count(),
                    AvgPricePerMeter = g.Where(f => f.PricePerMeter.HasValue).Any()
                        ? (decimal)g.Where(f => f.PricePerMeter.HasValue).Average(f => f.PricePerMeter.Value)
                        : 0m
                })
                .OrderBy(a => a.District)
                .ThenBy(a => a.RoomsCount)
                .ToList();

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
