namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;

public sealed class FlatPreviewRepository : IFlatPreviewRepository
{
    private readonly FlatsDbContext _dbContext;

    public FlatPreviewRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public Task<Dictionary<string, string>> GetImageUrlsAsync(string source, IReadOnlyCollection<string> listingIds, CancellationToken ct = default)
    {
        var ids = listingIds.Distinct().ToList();
        return _dbContext.FlatPreviews.AsNoTracking()
            .Where(p => p.Source == source && ids.Contains(p.ListingId))
            .ToDictionaryAsync(p => p.ListingId, p => p.ImageUrl, ct);
    }

    public async Task SaveAsync(IReadOnlyCollection<FlatPreview> previews, CancellationToken ct = default)
    {
        if (previews.Count == 0)
            return;

        var tracked = new List<FlatPreviewRecord>();
        foreach (var bySource in previews.GroupBy(p => p.Source))
        {
            var ids = bySource.Select(p => p.ListingId).Distinct().ToList();
            var existing = await _dbContext.FlatPreviews
                .Where(p => p.Source == bySource.Key && ids.Contains(p.ListingId))
                .ToDictionaryAsync(p => p.ListingId, ct);
            tracked.AddRange(existing.Values);
            var archived = (await _dbContext.FlatPreviewsArchive.AsNoTracking()
                .Where(p => p.Source == bySource.Key && ids.Contains(p.ListingId))
                .Select(p => p.ListingId)
                .ToListAsync(ct)).ToHashSet();

            foreach (var preview in bySource)
            {
                // The first photo of a flat goes into the archive as well, in the same SaveChanges; it stays there
                // when the portal changes the photo or the flat is removed
                if (archived.Add(preview.ListingId))
                {
                    var first = ArchivedFlatPreview.From(preview);
                    _dbContext.FlatPreviewsArchive.Add(first);
                    tracked.Add(first);
                }

                if (existing.TryGetValue(preview.ListingId, out var row))
                {
                    row.ImageUrl = preview.ImageUrl;
                    row.ContentType = preview.ContentType;
                    row.Data = preview.Data;
                    row.DownloadedAtUtc = preview.DownloadedAtUtc;
                }
                else
                {
                    _dbContext.FlatPreviews.Add(preview);
                    existing[preview.ListingId] = preview;
                    tracked.Add(preview);
                }
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            // The context is shared with the whole scrape: neither keep the bytes in memory nor retry a failed row
            foreach (var preview in tracked)
                _dbContext.Entry(preview).State = EntityState.Detached;
        }
    }

    public Task<FlatPreview?> GetAsync(string source, string listingId, CancellationToken ct = default) =>
        _dbContext.FlatPreviews.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Source == source && p.ListingId == listingId, ct);
}
