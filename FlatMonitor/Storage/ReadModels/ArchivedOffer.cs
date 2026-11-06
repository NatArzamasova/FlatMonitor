namespace FlatMonitor.Storage.ReadModels;

/// <summary>
/// A flat no longer offered, as the API serves it: in FlatsArchive, but not in FlatsSnapshots any more (sold,
/// delisted, or missed by a scrape). The <see cref="FlatOffer"/> fields are those of its latest archived run.
/// <see cref="FlatOffer.HasPreview"/> refers to its archived photo (FlatPreviewsArchive).
/// </summary>
public sealed class ArchivedOffer : FlatOffer
{
    /// <summary>When the flat was first seen: the earliest first-seen time (CreatedAtUtc) of its archived rows.</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>When the flat was last seen: when its latest archived run scraped it.</summary>
    public DateTime LastSeenUtc { get; set; }
}
