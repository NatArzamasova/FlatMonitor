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

        // Load only the rows of this batch, not every row of the source
        var existingMap = new Dictionary<(string Source, string ListingId), FlatSnapshot>();
        foreach (var bySource in listings.GroupBy(l => l.Source))
        {
            var source = bySource.Key;
            var ids = bySource.Select(l => l.ListingId).Distinct().ToList();
            var existing = await _dbContext.FlatsSnapshots
                .Where(s => s.Source == source && ids.Contains(s.ListingId))
                .ToListAsync(ct);
            foreach (var snapshot in existing)
                existingMap[(snapshot.Source, snapshot.ListingId)] = snapshot;
        }

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
        DetachSnapshots();
    }

    /// <summary>
    /// Stops tracking the saved snapshots so the tracker does not grow with every batch. Only snapshots
    /// (and their owned details) are detached: the context is shared, e.g. with the scrape history row.
    /// </summary>
    private void DetachSnapshots()
    {
        var snapshotEntries = _dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity is FlatSnapshot
                || e.Metadata.FindOwnership()?.PrincipalEntityType.ClrType == typeof(FlatSnapshot))
            .ToList();
        foreach (var entry in snapshotEntries)
            entry.State = EntityState.Detached;
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
