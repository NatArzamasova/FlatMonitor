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

        var tracked = new List<FlatPreview>();
        foreach (var bySource in previews.GroupBy(p => p.Source))
        {
            var ids = bySource.Select(p => p.ListingId).Distinct().ToList();
            var existing = await _dbContext.FlatPreviews
                .Where(p => p.Source == bySource.Key && ids.Contains(p.ListingId))
                .ToDictionaryAsync(p => p.ListingId, ct);
            tracked.AddRange(existing.Values);

            foreach (var preview in bySource)
            {
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
