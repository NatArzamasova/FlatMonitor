namespace workInProgress.Models;

public sealed class Flat
{
    public required string ListingId { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public string? District { get; init; }
    public int? PriceEur { get; init; }
    public int? OriginalPriceEur { get; init; }
    public int? PricePerMeter { get; init; }
    public double? RoomsCount { get; init; }
    public double? LivingAreaSqm { get; init; }
    public double? CommissionPercent { get; init; }
    public Details? Details { get; init; }
    public required string ScrapedAtUtc { get; init; }
    public string Source { get; init; } = "OhneMakler";
    /// <summary>
    /// Optional detail page URL used by some scrapers (e.g., OhneMakler) where detail URL differs from listing URL.
    /// </summary>
    public string? DetailPageUrl { get; init; }
}
