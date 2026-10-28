namespace FlatMonitor.Api.Contracts;

using FlatMonitor.Models;

public sealed record NormalizedFilterPreset(string Name, IReadOnlyList<string> Districts);

/// <summary>
/// Validates a <see cref="SaveFilterPresetRequest"/> and brings it into its stored form: trimmed name,
/// canonical district spellings, no duplicates, sorted.
/// </summary>
public static class FilterPresetRequestNormalizer
{
    /// <returns><c>true</c> with <paramref name="normalized"/> set, or <c>false</c> with <paramref name="error"/> set.</returns>
    public static bool TryNormalize(
        SaveFilterPresetRequest? request,
        out NormalizedFilterPreset normalized,
        out string error)
    {
        normalized = null!;
        error = "";

        if (request == null)
        {
            error = "Request body is required";
            return false;
        }

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            error = "Name is required";
            return false;
        }

        if (name.Length > FilterPreset.MaxNameLength)
        {
            error = $"Name must be at most {FilterPreset.MaxNameLength} characters";
            return false;
        }

        var requested = (request.Districts ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!.Trim())
            .ToList();

        var unknown = requested.Where(d => KarlsruheDistricts.ToCanonical(d) == null).ToList();
        if (unknown.Count > 0)
        {
            error = $"Unknown districts: {string.Join(", ", unknown)}";
            return false;
        }

        var districts = requested
            .Select(d => KarlsruheDistricts.ToCanonical(d)!)
            .Distinct()
            .Order(StringComparer.InvariantCulture)
            .ToList();

        normalized = new NormalizedFilterPreset(name, districts);
        return true;
    }
}
