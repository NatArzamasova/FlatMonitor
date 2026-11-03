namespace FlatMonitor.Storage;

using FlatMonitor.Models;

/// <summary>
/// The flats' title photos (FlatPreviews). Written during a scrape, one batch at a time; the previews of flats a
/// source no longer has are deleted when it is published (<see cref="IFlatRepository.PublishSourceAsync"/>).
/// </summary>
public interface IFlatPreviewRepository
{
    /// <summary>The stored image URL per listing id, for those of <paramref name="listingIds"/> that have a preview.</summary>
    Task<Dictionary<string, string>> GetImageUrlsAsync(string source, IReadOnlyCollection<string> listingIds, CancellationToken ct = default);

    /// <summary>Adds the previews, replacing a flat's existing one.</summary>
    Task SaveAsync(IReadOnlyCollection<FlatPreview> previews, CancellationToken ct = default);

    /// <summary>The flat's preview, or <c>null</c> if it has none.</summary>
    Task<FlatPreview?> GetAsync(string source, string listingId, CancellationToken ct = default);
}
