namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FlatMonitor.Storage.ReadModels;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class FlatsController : ApiControllerBase
{
    private readonly IFlatRepository _flatRepository;
    private readonly IFlatAggregationRepository _aggregationRepository;

    public FlatsController(IFlatRepository flatRepository, IFlatAggregationRepository aggregationRepository)
    {
        _flatRepository = flatRepository;
        _aggregationRepository = aggregationRepository;
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<FlatHistory>>>> GetHistoryAsync(CancellationToken ct = default)
    {
        var history = await _aggregationRepository.GetHistoryAsync(ct);
        return Ok(Success(history, "History fetched successfully"));
    }

    [HttpGet("current-offers")]
    public async Task<ActionResult<ApiResponse<List<FlatOffer>>>> GetCurrentOffersAsync(CancellationToken ct = default)
    {
        var offers = await _flatRepository.GetCurrentOffersAsync(ct);
        return Ok(Success(offers, "Current offers fetched successfully"));
    }

    [HttpGet("aggregated")]
    public async Task<ActionResult<ApiResponse<List<FlatAggregation>>>> GetAggregatedAsync(CancellationToken ct = default)
    {
        var aggregated = await _aggregationRepository.GetLatestAsync(ct);
        return Ok(Success(aggregated, "Aggregated successfully"));
    }
}
