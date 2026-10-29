namespace FlatMonitor.Storage.ReadModels;

/// <summary>One aggregation of the history; the API serves <see cref="ScrapingDate"/> as "yyyy-MM-dd".</summary>
public class FlatHistory
{
    public DateOnly ScrapingDate { get; set; }
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public decimal AvgPricePerMeter { get; set; }
    public int Count { get; set; }
}
