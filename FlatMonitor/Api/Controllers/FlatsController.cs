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
    private readonly IFlatPreviewRepository _previewRepository;

    public FlatsController(
        IFlatRepository flatRepository,
        IFlatAggregationRepository aggregationRepository,
        IFlatPreviewRepository previewRepository)
    {
        _flatRepository = flatRepository;
        _aggregationRepository = aggregationRepository;
        _previewRepository = previewRepository;
    }

    /// <summary>
    /// The flat's title photo (an offer with <see cref="FlatOffer.HasPreview"/>): the image itself, not an
    /// <see cref="ApiResponse{T}"/>; 404 without one. The browser may keep it for an hour.
    /// </summary>
    [HttpGet("{source}/{listingId}/preview")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetPreviewAsync(string source, string listingId, CancellationToken ct = default)
    {
        var preview = await _previewRepository.GetAsync(source, listingId, ct);
        if (preview == null || !FlatPreview.ContentTypes.Contains(preview.ContentType))
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(preview.Data, preview.ContentType);
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
