namespace workInProgress.Api.Models;

public class ScrapingStatusResponse
{
    public bool IsScrapingInProgress { get; set; }
    public ScrapingHistoryDto? LastScrapingHistory { get; set; }
}

public class ScrapingHistoryDto
{
    public int Id { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
}
