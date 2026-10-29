namespace FlatMonitor.Models;

/// <summary>A scraped flat. A record, so copies with changes use <c>flat with { ... }</c>.</summary>
public sealed record Flat
{
    public required string ListingId { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public string? District { get; init; }
    public int? PriceEur { get; init; }
    public int? OriginalPriceEur { get; init; }
    public double? RoomsCount { get; init; }
    public double? LivingAreaSqm { get; init; }
    public double? CommissionPercent { get; init; }
    public FlatDetails? Details { get; init; }
    public required DateTime ScrapedAtUtc { get; init; }
    public required string Source { get; init; }
    /// <summary>
    /// Optional detail page URL used by some scrapers (e.g., OhneMakler) where detail URL differs from listing URL.
    /// </summary>
    public string? DetailPageUrl { get; init; }

    /// <summary>Price per m² in whole euros; <c>null</c> without a price or a positive living area.</summary>
    public int? PricePerMeter =>
        PriceEur is { } price && LivingAreaSqm is { } area && area > 0
            ? (int)Math.Round(price / area)
            : null;
}
