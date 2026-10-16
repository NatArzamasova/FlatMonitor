namespace workInProgress.Models;

public sealed class FlatSnapshot
{
    public required string Source { get; init; }
    public required string ListingId { get; init; }
    public string? Url { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? District { get; set; }
    public int? PriceEur { get; set; }
    public int? OriginalPriceEur { get; set; }
    public int? PricePerMeter { get; set; }
    public double? RoomsCount { get; set; }
    public double? LivingAreaSqm { get; set; }
    public FlatDetails? Details { get; set; }
    public required string ScrapedAtUtc { get; set; }
}
