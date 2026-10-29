namespace FlatMonitor.Storage.ReadModels;

/// <summary>A flat of FlatsSnapshots as the API serves it.</summary>
public class FlatOffer
{
    public string? District { get; set; }
    public double? RoomsCount { get; set; }
    public int? PriceEur { get; init; }
    public int? OriginalPriceEur { get; init; }
    public int? PricePerMeter { get; set; }
    public double? LivingAreaSqm { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
}
