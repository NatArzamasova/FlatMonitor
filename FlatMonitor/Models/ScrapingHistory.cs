namespace FlatMonitor.Models;

public class ScrapingHistory
{
    public int Id { get; set; }
    public required DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public required string Status { get; set; } // "Running", "Successful", "Failed", "Canceled"
    public string? ErrorMessage { get; set; }
    public int FlatsScrapedCount { get; set; } = 0;
}
