namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;

public sealed class FlatSnapshotRepository : ISnapshotRepository
{
    private readonly FlatsDbContext _dbContext;

    public FlatSnapshotRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public Task ClearAsync(CancellationToken ct = default) =>
        _dbContext.FlatsSnapshots.ExecuteDeleteAsync(ct);

    public Task BackupFlatsSnapshotsAsync(CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlRawAsync(@"
            DROP TABLE IF EXISTS FlatsSnapshots_Backup;
            CREATE TABLE FlatsSnapshots_Backup AS SELECT * FROM FlatsSnapshots;", ct);

    public Task RestoreFlatsSnapshotsAsync(CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlRawAsync(@"
            DELETE FROM FlatsSnapshots;
            INSERT INTO FlatsSnapshots SELECT * FROM FlatsSnapshots_Backup;", ct);

    public Task DeleteBackupAsync(CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS FlatsSnapshots_Backup;", ct);

    public async Task UpsertListingsAsync(List<Flat> listings, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var incomingSources = listings.Select(l => l.Source).Distinct().ToList();
        var existingSnapshots = await _dbContext.FlatsSnapshots
            .Where(s => incomingSources.Contains(s.Source))
            .ToListAsync(ct);

        // Build map of existing by (Source, ListingId) key
        var existingMap = existingSnapshots.ToDictionary(s => (s.Source, s.ListingId));

        foreach (var listing in listings)
        {
            var key = (listing.Source, listing.ListingId);

            if (existingMap.TryGetValue(key, out var existing))
            {
                existing.UpdateFrom(listing);
                _dbContext.Entry(existing).Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
            }
            else
            {
                var snapshot = FlatSnapshot.From(listing);
                var entry = _dbContext.Entry(snapshot);
                entry.Property<DateTime>("CreatedAtUtc").CurrentValue = now;
                entry.Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
                _dbContext.FlatsSnapshots.Add(snapshot);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<FlatOffer>> GetCurrentOffersAsync(CancellationToken ct = default)
    {
        return await _dbContext.FlatsSnapshots
                .OrderByDescending(s => s.ScrapedAtUtc)
                .Select(s => new FlatOffer
                {
                    District = s.District,
                    RoomsCount = s.RoomsCount,
                    PriceEur = s.PriceEur,
                    OriginalPriceEur = s.OriginalPriceEur,
                    PricePerMeter = s.PricePerMeter,
                    LivingAreaSqm = s.LivingAreaSqm,
                    Url = s.Url,
                    Source = s.Source
                })
                .ToListAsync(ct);
    }

}
