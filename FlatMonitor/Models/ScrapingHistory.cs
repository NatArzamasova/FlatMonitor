namespace FlatMonitor.Models;

using System.Text.Json.Serialization;

public class ScrapingHistory
{
    public int Id { get; set; }
    public required DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public required ScrapingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int FlatsScrapedCount { get; set; } = 0;
}

/// <summary>State of a scrape run. Stored and serialized by name ("Running", "Successful", ...).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScrapingStatus>))]
public enum ScrapingStatus
{
    Running,
    Successful,
    /// <summary>Finished, but at least one source failed.</summary>
    PartialFailed,
    Failed,
    Canceled,
}
