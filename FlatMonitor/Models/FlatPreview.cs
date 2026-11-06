namespace FlatMonitor.Models;

/// <summary>
/// A flat's title photo as stored in a previews table. Both tables have the same columns and key:
/// <see cref="FlatPreview"/> (FlatPreviews, the photos of the current flats) and <see cref="ArchivedFlatPreview"/>
/// (FlatPreviewsArchive, one photo per flat ever seen).
/// </summary>
public abstract class FlatPreviewRecord
{
    public required string Source { get; init; }
    public required string ListingId { get; init; }

    /// <summary>Where the image was downloaded from; an unchanged URL is not downloaded again.</summary>
    public required string ImageUrl { get; set; }

    /// <summary>The response's media type, e.g. <c>image/jpeg</c> or <c>image/webp</c>.</summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTime DownloadedAtUtc { get; set; }
}

/// <summary>
/// A flat's title photo at preview size (about 400×300, 10–30 KB), shown when hovering over an offer. One row per flat
/// of FlatsSnapshots, same key; kept in its own table so reading the offers never loads the image bytes.
/// Written while a source is scraped; publishing the source deletes the previews of flats it no longer has.
/// </summary>
public sealed class FlatPreview : FlatPreviewRecord
{
    /// <summary>Larger downloads are not stored: a preview-sized photo is far smaller, so it is not one.</summary>
    public const int MaxBytes = 512 * 1024;

    /// <summary>
    /// The photo formats stored. The API serves the bytes from its own origin with the stored type, so no SVG or
    /// HTML (script) from a portal's server can get in.
    /// </summary>
    public static readonly IReadOnlySet<string> ContentTypes =
        new HashSet<string> { "image/jpeg", "image/png", "image/webp", "image/gif", "image/avif" };
}

/// <summary>
/// A row of the FlatPreviewsArchive table: the photo a flat was first seen with, stored once per (Source, ListingId)
/// and never replaced or deleted. It belongs to all of the flat's FlatsArchive rows (same Source and ListingId).
/// </summary>
public sealed class ArchivedFlatPreview : FlatPreviewRecord
{
    public static ArchivedFlatPreview From(FlatPreviewRecord preview) => new()
    {
        Source = preview.Source,
        ListingId = preview.ListingId,
        ImageUrl = preview.ImageUrl,
        ContentType = preview.ContentType,
        Data = preview.Data,
        DownloadedAtUtc = preview.DownloadedAtUtc,
    };
}
