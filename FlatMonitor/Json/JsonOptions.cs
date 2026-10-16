namespace workInProgress.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class JsonOptions
{
    // PascalCase, matching the exact field names documented in ohneMaklerMonitor.md
    // (ListingId, PriceEur, etc.) — System.Text.Json defaults to emitting C# property
    // names as-is when no naming policy is set, so this is mostly "explicitly no camelCase".
    public static readonly JsonSerializerOptions Snapshot = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };
}
