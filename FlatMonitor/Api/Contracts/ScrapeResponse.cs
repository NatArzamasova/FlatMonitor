namespace FlatMonitor.Api.Contracts;

public class ScrapeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public DateTime ScrapedAtUtc { get; set; }
    public int FlatsCount { get; set; }
}
