namespace FlatMonitor.Models;

/// <summary>
/// A flat's title photo at preview size (about 400×300, 10–30 KB), shown when hovering over an offer. One row per flat
/// of FlatsSnapshots, same key; kept in its own table so reading the offers never loads the image bytes.
/// Written while a source is scraped; publishing the source deletes the previews of flats it no longer has.
/// </summary>
public sealed class FlatPreview
{
    /// <summary>Larger downloads are not stored: a preview-sized photo is far smaller, so it is not one.</summary>
    public const int MaxBytes = 512 * 1024;

    /// <summary>
    /// The photo formats stored. The API serves the bytes from its own origin with the stored type, so no SVG or
    /// HTML (script) from a portal's server can get in.
    /// </summary>
    public static readonly IReadOnlySet<string> ContentTypes =
        new HashSet<string> { "image/jpeg", "image/png", "image/webp", "image/gif", "image/avif" };

    public required string Source { get; init; }
    public required string ListingId { get; init; }

    /// <summary>Where the image was downloaded from; an unchanged URL is not downloaded again.</summary>
    public required string ImageUrl { get; set; }

    /// <summary>The response's media type, e.g. <c>image/jpeg</c> or <c>image/webp</c>.</summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTime DownloadedAtUtc { get; set; }
}
