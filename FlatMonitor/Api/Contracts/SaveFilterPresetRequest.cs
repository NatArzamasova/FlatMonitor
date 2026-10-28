namespace FlatMonitor.Api.Contracts;

/// <summary>Body for creating or updating a filter preset; normalised by <c>FilterPresetRequestNormalizer</c>.</summary>
public class SaveFilterPresetRequest
{
    public string? Name { get; set; }

    /// <summary>District names; <c>null</c> or empty means all districts.</summary>
    public List<string?>? Districts { get; set; }
}
