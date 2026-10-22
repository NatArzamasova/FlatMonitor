namespace FlatMonitor.Api.Models;

public class FlatAggregation
{
    public string? PostalCode { get; set; }
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public string? Availability { get; set; }
    public int Count { get; set; }
    public decimal AvgPricePerMeter { get; set; }
}
