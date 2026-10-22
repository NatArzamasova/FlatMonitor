namespace FlatMonitor.Api.Contracts;

using FlatMonitor.Models;

public class ScrapingStatusResponse
{
    public bool IsScrapingInProgress { get; set; }
    public ScrapingHistory? LastScrapingHistory { get; set; }
    public ScrapingHistory? LastSuccessfulScrapingHistory { get; set; }
}
