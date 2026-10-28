namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/filter-presets")]
public class FilterPresetsController : ControllerBase
{
    private readonly IFilterPresetRepository _repository;

    public FilterPresetsController(IFilterPresetRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> GetAllAsync()
    {
        try
        {
            return Ok(Success(await GetAllDtosAsync(), "Filter presets fetched successfully"));
        }
        catch (Exception ex)
        {
            return ServerError<List<FilterPresetDto>>($"Error fetching filter presets: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> CreateAsync([FromBody] SaveFilterPresetRequest? request)
    {
        if (!FilterPresetRequestNormalizer.TryNormalize(request, out var preset, out var error))
            return BadRequest(Failure<FilterPresetDto>(error));

        try
        {
            if (await _repository.NameExistsAsync(preset.Name))
                return NameTaken(preset.Name);

            var created = await _repository.CreateAsync(preset.Name, preset.Districts);
            return StatusCode(StatusCodes.Status201Created, Success(FilterPresetDto.From(created), "Filter preset created"));
        }
        catch (DbUpdateException ex)
        {
            // A concurrent request may have taken the name between the check and the insert.
            return await _repository.NameExistsAsync(preset.Name)
                ? NameTaken(preset.Name)
                : ServerError<FilterPresetDto>($"Error creating filter preset: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ServerError<FilterPresetDto>($"Error creating filter preset: {ex.Message}");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> UpdateAsync(int id, [FromBody] SaveFilterPresetRequest? request)
    {
        if (!FilterPresetRequestNormalizer.TryNormalize(request, out var preset, out var error))
            return BadRequest(Failure<FilterPresetDto>(error));

        try
        {
            if (await _repository.NameExistsAsync(preset.Name, excludingId: id))
                return NameTaken(preset.Name);

            var updated = await _repository.UpdateAsync(id, preset.Name, preset.Districts);
            return updated == null
                ? PresetNotFound<FilterPresetDto>(id)
                : Ok(Success(FilterPresetDto.From(updated), "Filter preset updated"));
        }
        catch (DbUpdateException ex)
        {
            return await _repository.NameExistsAsync(preset.Name, excludingId: id)
                ? NameTaken(preset.Name)
                : ServerError<FilterPresetDto>($"Error updating filter preset: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ServerError<FilterPresetDto>($"Error updating filter preset: {ex.Message}");
        }
    }

    /// <summary>Deletes the preset and returns the remaining presets.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> DeleteAsync(int id)
    {
        try
        {
            if (!await _repository.DeleteAsync(id))
                return PresetNotFound<List<FilterPresetDto>>(id);

            return Ok(Success(await GetAllDtosAsync(), "Filter preset deleted"));
        }
        catch (Exception ex)
        {
            return ServerError<List<FilterPresetDto>>($"Error deleting filter preset: {ex.Message}");
        }
    }

    [HttpPut("{id:int}/default")]
    public async Task<ActionResult<ApiResponse<FilterPresetDto>>> SetDefaultAsync(int id)
    {
        try
        {
            var preset = await _repository.SetDefaultAsync(id);
            return preset == null
                ? PresetNotFound<FilterPresetDto>(id)
                : Ok(Success(FilterPresetDto.From(preset), "Default filter preset set"));
        }
        catch (Exception ex)
        {
            return ServerError<FilterPresetDto>($"Error setting default filter preset: {ex.Message}");
        }
    }

    /// <summary>Leaves no preset as default and returns all presets.</summary>
    [HttpDelete("default")]
    public async Task<ActionResult<ApiResponse<List<FilterPresetDto>>>> ClearDefaultAsync()
    {
        try
        {
            await _repository.ClearDefaultAsync();
            return Ok(Success(await GetAllDtosAsync(), "Default filter preset cleared"));
        }
        catch (Exception ex)
        {
            return ServerError<List<FilterPresetDto>>($"Error clearing default filter preset: {ex.Message}");
        }
    }

    private async Task<List<FilterPresetDto>> GetAllDtosAsync()
    {
        var presets = await _repository.GetAllAsync();
        return presets.Select(FilterPresetDto.From).ToList();
    }

    private ObjectResult NameTaken(string name) =>
        Conflict(Failure<FilterPresetDto>($"A filter preset named '{name}' already exists"));

    private ObjectResult PresetNotFound<T>(int id) =>
        NotFound(Failure<T>($"Filter preset {id} not found"));

    private ObjectResult ServerError<T>(string message) =>
        StatusCode(StatusCodes.Status500InternalServerError, Failure<T>(message));

    private static ApiResponse<T> Success<T>(T data, string message) =>
        new() { Success = true, Data = data, Message = message };

    private static ApiResponse<T> Failure<T>(string message) =>
        new() { Success = false, Message = message };
}
