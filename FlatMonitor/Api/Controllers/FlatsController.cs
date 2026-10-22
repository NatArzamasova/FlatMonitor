namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Models;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class FlatsController : ControllerBase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFlatAggregationRepository _aggregationRepository;

    public FlatsController(ISnapshotRepository snapshotRepository, IFlatAggregationRepository aggregationRepository)
    {
        _snapshotRepository = snapshotRepository;
        _aggregationRepository = aggregationRepository;
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<FlatHistoryDto>>>> GetHistoryAsync()
    {
        try
        {
            var history = await _aggregationRepository.GetHistoryAsync();

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
            var offers = await _snapshotRepository.GetCurrentOffersAsync();

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
            var aggregated = await _aggregationRepository.GetLatestAsync();

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
