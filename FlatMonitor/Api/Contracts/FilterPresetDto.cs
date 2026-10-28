namespace FlatMonitor.Api.Contracts;

using FlatMonitor.Models;

public class FilterPresetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Districts { get; set; } = [];
    public bool IsDefault { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static FilterPresetDto From(FilterPreset preset) => new()
    {
        Id = preset.Id,
        Name = preset.Name,
        Districts = [.. preset.Districts],
        IsDefault = preset.IsDefault,
        UpdatedAtUtc = preset.UpdatedAtUtc
    };
}
