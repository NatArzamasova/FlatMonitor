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

    /// <summary>
    /// The sources the run scraped, comma-separated (e.g. <c>OhneMakler,ImmobilienScout24</c>); <c>null</c> for runs
    /// from before it was recorded.
    /// </summary>
    public string? Sources { get; set; }

    /// <summary><see cref="Sources"/> as a list; <c>null</c> if not recorded.</summary>
    public IReadOnlyList<string>? SourceList => Sources?.Split(',', StringSplitOptions.RemoveEmptyEntries);
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
