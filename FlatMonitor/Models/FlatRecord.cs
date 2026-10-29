namespace FlatMonitor.Models;

/// <summary>
/// A <see cref="Flat"/> as stored in a flats table. Both tables have the same columns:
/// <see cref="PublishedFlat"/> (FlatsSnapshots, what the API serves) and <see cref="StagedFlat"/>
/// (FlatsSnapshots_Staging, the running scrape until its sources are published).
/// </summary>
public abstract class FlatRecord
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

    /// <summary>Copies every stored field from <paramref name="flat"/>; the key (Source, ListingId) must match.</summary>
    public void UpdateFrom(Flat flat)
    {
        EnsureSameKey(flat.Source, flat.ListingId);

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

    /// <summary>Copies every stored field from <paramref name="record"/>; the key (Source, ListingId) must match.</summary>
    public void UpdateFrom(FlatRecord record)
    {
        EnsureSameKey(record.Source, record.ListingId);

        Url = record.Url;
        Title = record.Title;
        Address = record.Address;
        PostalCode = record.PostalCode;
        District = record.District;
        PriceEur = record.PriceEur;
        OriginalPriceEur = record.OriginalPriceEur;
        PricePerMeter = record.PricePerMeter;
        RoomsCount = record.RoomsCount;
        LivingAreaSqm = record.LivingAreaSqm;
        CommissionPercent = record.CommissionPercent;
        Details = record.Details;
        ScrapedAtUtc = record.ScrapedAtUtc;
    }

    private void EnsureSameKey(string source, string listingId)
    {
        if (source != Source || listingId != ListingId)
            throw new ArgumentException($"Flat {source}/{listingId} does not belong to row {Source}/{ListingId}");
    }
}

/// <summary>A row of the FlatsSnapshots table: a flat of the latest published scrape of its source.</summary>
public sealed class PublishedFlat : FlatRecord
{
    public static PublishedFlat From(Flat flat)
    {
        var published = new PublishedFlat { Source = flat.Source, ListingId = flat.ListingId, ScrapedAtUtc = flat.ScrapedAtUtc };
        published.UpdateFrom(flat);
        return published;
    }

    public static PublishedFlat From(FlatRecord record)
    {
        var published = new PublishedFlat { Source = record.Source, ListingId = record.ListingId, ScrapedAtUtc = record.ScrapedAtUtc };
        published.UpdateFrom(record);
        return published;
    }
}

/// <summary>A row of the FlatsSnapshots_Staging table: a flat of the running scrape, not yet published.</summary>
public sealed class StagedFlat : FlatRecord
{
    public static StagedFlat From(Flat flat)
    {
        var staged = new StagedFlat { Source = flat.Source, ListingId = flat.ListingId, ScrapedAtUtc = flat.ScrapedAtUtc };
        staged.UpdateFrom(flat);
        return staged;
    }
}
