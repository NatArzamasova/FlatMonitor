namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Models;
using FlatMonitor.Models;
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
    public async Task<ActionResult<ApiResponse<List<FlatHistory>>>> GetHistoryAsync()
    {
        try
        {
            var history = await _aggregationRepository.GetHistoryAsync();

            return Ok(new ApiResponse<List<FlatHistory>>
            {
                Success = true,
                Data = history,
                Message = "History fetched successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<List<FlatHistory>>
            {
                Success = false,
                Message = $"Error fetching history: {ex.Message}"
            });
        }
    }

    [HttpGet("current-offers")]
    public async Task<ActionResult<ApiResponse<List<FlatOffer>>>> GetCurrentOffersAsync()
    {
        try
        {
            var offers = await _snapshotRepository.GetCurrentOffersAsync();

            return Ok(new ApiResponse<List<FlatOffer>>
            {
                Success = true,
                Data = offers,
                Message = "Current offers fetched successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<List<FlatOffer>>
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
