namespace FlatMonitor.Api.Contracts;

public class ScrapingStatusResponse
{
    public bool IsScrapingInProgress { get; set; }
    public ScrapingHistoryDto? LastScrapingHistory { get; set; }
    public ScrapingHistoryDto? LastSuccessfulScrapingHistory { get; set; }
}
