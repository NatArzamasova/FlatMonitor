namespace FlatMonitor.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class JsonOptions
{
    // The JSONL backup files (JsonlBackupWriter): PascalCase, the Flat property names as they are
    // (ListingId, PriceEur, etc.) — System.Text.Json emits C# property names unchanged when no
    // naming policy is set, so this is mostly "explicitly no camelCase".
    public static readonly JsonSerializerOptions JsonlBackup = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };
}
