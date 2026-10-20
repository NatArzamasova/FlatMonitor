namespace workInProgress.Api.Models;

public class FlatHistoryDto
{
    public string? ScrapingDate { get; set; }
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public decimal AvgPricePerMeter { get; set; }
    public int Count { get; set; }
}
