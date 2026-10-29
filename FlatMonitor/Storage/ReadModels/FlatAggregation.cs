namespace FlatMonitor.Storage.ReadModels;

/// <summary>One aggregation of the latest scraping date as the API serves it.</summary>
public class FlatAggregation
{
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public int Count { get; set; }
    public decimal AvgPricePerMeter { get; set; }
}
