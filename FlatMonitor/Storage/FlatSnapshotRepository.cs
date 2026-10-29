namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public sealed class FlatSnapshotRepository : ISnapshotRepository
{
    private readonly FlatsDbContext _dbContext;

    public FlatSnapshotRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public Task BeginRunAsync(CancellationToken ct = default) =>
        _dbContext.FlatsSnapshotsStaging.ExecuteDeleteAsync(ct);

    public Task DiscardRunAsync(CancellationToken ct = default) =>
        _dbContext.FlatsSnapshotsStaging.ExecuteDeleteAsync(ct);

    public async Task AddToRunAsync(List<Flat> flats, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Load only the rows of this batch, not every row of the source
        var existingMap = new Dictionary<(string Source, string ListingId), StagedFlatSnapshot>();
        foreach (var bySource in flats.GroupBy(l => l.Source))
        {
            var source = bySource.Key;
            var ids = bySource.Select(l => l.ListingId).Distinct().ToList();
            var existing = await _dbContext.FlatsSnapshotsStaging
                .Where(s => s.Source == source && ids.Contains(s.ListingId))
                .ToListAsync(ct);
            foreach (var staged in existing)
                existingMap[(staged.Source, staged.ListingId)] = staged;
        }

        foreach (var flat in flats)
        {
            var key = (flat.Source, flat.ListingId);

            if (existingMap.TryGetValue(key, out var existing))
            {
                existing.UpdateFrom(flat);
                _dbContext.Entry(existing).Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
            }
            else
            {
                var staged = StagedFlatSnapshot.From(flat);
                SetAuditDates(_dbContext.Entry(staged), now);
                _dbContext.FlatsSnapshotsStaging.Add(staged);
                existingMap[key] = staged;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        DetachFlatRows();
    }

    public async Task<int> PublishSourceAsync(string source, CancellationToken ct = default)
    {
        var staged = await _dbContext.FlatsSnapshotsStaging.AsNoTracking()
            .Where(s => s.Source == source)
            .ToListAsync(ct);
        var removed = await _dbContext.FlatsSnapshots
            .Where(s => s.Source == source)
            .ToDictionaryAsync(s => s.ListingId, ct);
        var now = DateTime.UtcNow;

        // Update the flats still offered, add the new ones, delete the rest: one SaveChanges, so one transaction
        foreach (var row in staged)
        {
            if (removed.Remove(row.ListingId, out var existing))
            {
                existing.UpdateFrom(row);
                _dbContext.Entry(existing).Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
            }
            else
            {
                var snapshot = FlatSnapshot.From(row);
                SetAuditDates(_dbContext.Entry(snapshot), now);
                _dbContext.FlatsSnapshots.Add(snapshot);
            }
        }
        _dbContext.FlatsSnapshots.RemoveRange(removed.Values);

        await _dbContext.SaveChangesAsync(ct);
        DetachFlatRows();
        return staged.Count;
    }

    private static void SetAuditDates(EntityEntry entry, DateTime now)
    {
        entry.Property("CreatedAtUtc").CurrentValue = now;
        entry.Property("UpdatedAtUtc").CurrentValue = now;
    }

    /// <summary>
    /// Stops tracking the saved flat rows so the tracker does not grow with every batch. Only flat rows
    /// (and their owned details) are detached: the context is shared, e.g. with the scrape history row.
    /// </summary>
    private void DetachFlatRows()
    {
        var flatEntries = _dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity is FlatRecord
                || e.Metadata.FindOwnership()?.PrincipalEntityType.ClrType.IsAssignableTo(typeof(FlatRecord)) == true)
            .ToList();
        foreach (var entry in flatEntries)
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
