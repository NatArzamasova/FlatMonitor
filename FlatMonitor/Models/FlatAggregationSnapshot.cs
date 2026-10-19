namespace workInProgress.Models;

public sealed class FlatAggregationSnapshot
{
    public int Id { get; set; }
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public int Count { get; set; }
    public decimal AvgPricePerMeter { get; set; }
    public DateOnly ScrapingDate { get; set; }
}
