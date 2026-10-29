namespace FlatMonitor.Models;

/// <summary>A <see cref="Flat"/> as stored in the FlatsSnapshots table.</summary>
public sealed class FlatSnapshot
{
    public required string Source { get; init; }
    public required string ListingId { get; init; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? District { get; set; }
    public int? PriceEur { get; set; }
    public int? OriginalPriceEur { get; set; }
    public int? PricePerMeter { get; set; }
    public double? RoomsCount { get; set; }
    public double? LivingAreaSqm { get; set; }
    public double? CommissionPercent { get; set; }
    public FlatDetails? Details { get; set; }
    public required DateTime ScrapedAtUtc { get; set; }

    public static FlatSnapshot From(Flat flat)
    {
        var snapshot = new FlatSnapshot { Source = flat.Source, ListingId = flat.ListingId, ScrapedAtUtc = flat.ScrapedAtUtc };
        snapshot.UpdateFrom(flat);
        return snapshot;
    }

    /// <summary>Copies every stored field from <paramref name="flat"/>; the key (Source, ListingId) must match.</summary>
    public void UpdateFrom(Flat flat)
    {
        if (flat.Source != Source || flat.ListingId != ListingId)
            throw new ArgumentException($"Flat {flat.Source}/{flat.ListingId} does not belong to snapshot {Source}/{ListingId}", nameof(flat));

        Url = flat.Url;
        Title = flat.Title;
        Address = flat.Address;
        PostalCode = flat.PostalCode;
        District = flat.District;
        PriceEur = flat.PriceEur;
        OriginalPriceEur = flat.OriginalPriceEur;
        PricePerMeter = flat.PricePerMeter;
        RoomsCount = flat.RoomsCount;
        LivingAreaSqm = flat.LivingAreaSqm;
        CommissionPercent = flat.CommissionPercent;
        Details = flat.Details;
        ScrapedAtUtc = flat.ScrapedAtUtc;
    }
}
