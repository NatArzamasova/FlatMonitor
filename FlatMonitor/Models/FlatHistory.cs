namespace FlatMonitor.Models;

public class FlatHistory
{
    public string? ScrapingDate { get; set; }
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public decimal AvgPricePerMeter { get; set; }
    public int Count { get; set; }
}
