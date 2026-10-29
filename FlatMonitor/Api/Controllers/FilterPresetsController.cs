namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/filter-presets")]
public class FilterPresetsController : ApiControllerBase
{
    private readonly IFilterPresetRepository _repository;

    public FilterPresetsController(IFilterPresetRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> GetAllAsync(CancellationToken ct = default)
    {
        return Ok(Success(await GetAllDtosAsync(ct), "Filter presets fetched successfully"));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> CreateAsync([FromBody] SaveFilterPresetRequest? request, CancellationToken ct = default)
    {
        if (!FilterPresetRequestNormalizer.TryNormalize(request, out var preset, out var error))
            return BadRequest(Failure<FilterPresetDto>(error));

        try
        {
            if (await _repository.NameExistsAsync(preset.Name, cancellationToken: ct))
                return NameTaken(preset.Name);

            var created = await _repository.CreateAsync(preset.Name, preset.Districts, ct);
            return StatusCode(StatusCodes.Status201Created, Success(FilterPresetDto.From(created), "Filter preset created"));
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have taken the name between the check and the insert.
            if (await _repository.NameExistsAsync(preset.Name, cancellationToken: ct))
                return NameTaken(preset.Name);
            throw;
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> UpdateAsync(int id, [FromBody] SaveFilterPresetRequest? request, CancellationToken ct = default)
    {
        if (!FilterPresetRequestNormalizer.TryNormalize(request, out var preset, out var error))
            return BadRequest(Failure<FilterPresetDto>(error));

        try
        {
            if (await _repository.NameExistsAsync(preset.Name, excludingId: id, cancellationToken: ct))
                return NameTaken(preset.Name);

            var updated = await _repository.UpdateAsync(id, preset.Name, preset.Districts, ct);
            return updated == null
                ? PresetNotFound<FilterPresetDto>(id)
                : Ok(Success(FilterPresetDto.From(updated), "Filter preset updated"));
        }
        catch (DbUpdateException)
        {
            if (await _repository.NameExistsAsync(preset.Name, excludingId: id, cancellationToken: ct))
                return NameTaken(preset.Name);
            throw;
        }
    }

    /// <summary>Deletes the preset and returns the remaining presets.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!await _repository.DeleteAsync(id, ct))
            return PresetNotFound<List<FilterPresetDto>>(id);

        return Ok(Success(await GetAllDtosAsync(ct), "Filter preset deleted"));
    }

    [HttpPut("{id:int}/default")]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> SetDefaultAsync(int id, CancellationToken ct = default)
    {
        var preset = await _repository.SetDefaultAsync(id, ct);
        return preset == null
            ? PresetNotFound<FilterPresetDto>(id)
            : Ok(Success(FilterPresetDto.From(preset), "Default filter preset set"));
    }

    /// <summary>Leaves no preset as default and returns all presets.</summary>
    [HttpDelete("default")]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> ClearDefaultAsync(CancellationToken ct = default)
    {
        await _repository.ClearDefaultAsync(ct);
        return Ok(Success(await GetAllDtosAsync(ct), "Default filter preset cleared"));
    }

    private async Task<List<FilterPresetDto>> GetAllDtosAsync(CancellationToken ct = default)
    {
        var presets = await _repository.GetAllAsync(ct);
        return presets.Select(FilterPresetDto.From).ToList();
    }

    private ObjectResult NameTaken(string name) =>
        Conflict(Failure<FilterPresetDto>($"A filter preset named '{name}' already exists"));

    private ObjectResult PresetNotFound<T>(int id) =>
        NotFound(Failure<T>($"Filter preset {id} not found"));
}
