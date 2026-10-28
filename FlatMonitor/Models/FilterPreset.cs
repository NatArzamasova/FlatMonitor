namespace FlatMonitor.Models;

/// <summary>
/// A named, saved dashboard filter. Any number of presets can exist; at most one has
/// <see cref="IsDefault"/> set, and that one is applied when the dashboard opens.
/// </summary>
public sealed class FilterPreset
{
    public const int MaxNameLength = 100;

    public int Id { get; set; }

    /// <summary>Unique (case-insensitive) display name.</summary>
    public required string Name { get; set; }

    /// <summary>District selection; empty means all districts.</summary>
    public List<string> Districts { get; set; } = [];

    public bool IsDefault { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
