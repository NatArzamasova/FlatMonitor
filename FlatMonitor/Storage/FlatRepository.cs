namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using FlatMonitor.Storage.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public sealed class FlatRepository : IFlatRepository
{
    private readonly FlatsDbContext _dbContext;

    public FlatRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public Task BeginRunAsync(CancellationToken ct = default) =>
        _dbContext.FlatsSnapshotsStaging.ExecuteDeleteAsync(ct);

    public Task DiscardRunAsync(CancellationToken ct = default) =>
        _dbContext.FlatsSnapshotsStaging.ExecuteDeleteAsync(ct);

    public async Task AddToRunAsync(List<Flat> flats, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Load only the rows of this batch, not every row of the source
        var existingMap = new Dictionary<(string Source, string ListingId), StagedFlat>();
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
                var staged = StagedFlat.From(flat);
                SetAuditDates(_dbContext.Entry(staged), now);
                _dbContext.FlatsSnapshotsStaging.Add(staged);
                existingMap[key] = staged;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            // Also after a failed save: rows left tracked would be inserted again by the next SaveChanges on the
            // shared context (the next source's batch, the aggregations), failing the whole run
            DetachFlatRows();
        }
    }

    public async Task<int> PublishSourceAsync(string source, int scrapingHistoryId, CancellationToken ct = default)
    {
        var staged = await _dbContext.FlatsSnapshotsStaging.AsNoTracking()
            .Where(s => s.Source == source)
            .ToListAsync(ct);
        var removed = await _dbContext.FlatsSnapshots
            .Where(s => s.Source == source)
            .ToDictionaryAsync(s => s.ListingId, ct);
        var now = DateTime.UtcNow;

        // Update the flats still offered, add the new ones, delete the rest, and archive every published flat with the
        // run: one SaveChanges, so one transaction. The archive always holds exactly what was published.
        foreach (var row in staged)
        {
            PublishedFlat published;
            if (removed.Remove(row.ListingId, out var existing))
            {
                existing.UpdateFrom(row);
                _dbContext.Entry(existing).Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
                published = existing;
            }
            else
            {
                published = PublishedFlat.From(row);
                SetAuditDates(_dbContext.Entry(published), now);
                _dbContext.FlatsSnapshots.Add(published);
            }

            Archive(published, scrapingHistoryId, now);
        }
        _dbContext.FlatsSnapshots.RemoveRange(removed.Values);
        await RemoveStalePreviewsAsync(source, staged.Select(row => row.ListingId).ToHashSet(), ct);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            // Also after a failed save: the other sources go on with the shared context, and its next SaveChanges
            // would write this source's rows (archive rows included) after all, or fail on them again
            DetachFlatRows();
            DetachPreviews();
        }
        return staged.Count;
    }

    /// <summary>
    /// Marks the source's previews of flats not in the published run for deletion, in the publish's SaveChanges: those
    /// of removed flats, and those a failed earlier run downloaded for flats it never published.
    /// </summary>
    private async Task RemoveStalePreviewsAsync(string source, HashSet<string> publishedIds, CancellationToken ct)
    {
        var previewIds = await _dbContext.FlatPreviews
            .Where(p => p.Source == source)
            .Select(p => p.ListingId)
            .ToListAsync(ct);
        var staleIds = previewIds.Where(id => !publishedIds.Contains(id)).ToList();
        if (staleIds.Count == 0)
            return;

        var stale = await _dbContext.FlatPreviews
            .Where(p => p.Source == source && staleIds.Contains(p.ListingId))
            .ToListAsync(ct);
        _dbContext.FlatPreviews.RemoveRange(stale);
    }

    /// <summary>
    /// Adds the published flat to FlatsArchive as of this run. Its CreatedAtUtc is the published row's, so it keeps
    /// meaning "first seen"; UpdatedAtUtc is when it was archived.
    /// </summary>
    private void Archive(PublishedFlat published, int scrapingHistoryId, DateTime now)
    {
        var archived = ArchivedFlat.From(published, scrapingHistoryId);
        var entry = _dbContext.FlatsArchive.Add(archived);
        entry.Property<DateTime>("CreatedAtUtc").CurrentValue =
            _dbContext.Entry(published).Property<DateTime>("CreatedAtUtc").CurrentValue;
        entry.Property<DateTime>("UpdatedAtUtc").CurrentValue = now;
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

    /// <summary>Stops tracking the previews a publish marked for deletion; after a successful save there are none.</summary>
    private void DetachPreviews()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<FlatPreview>().ToList())
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
                    Source = s.Source,
                    ListingId = s.ListingId,
                    HasPreview = _dbContext.FlatPreviews.Any(p => p.Source == s.Source && p.ListingId == s.ListingId),
                    Availability = s.Details == null ? null : s.Details.Availability,
                    EnergyEfficiencyClass = s.Details == null ? null : s.Details.EnergyEfficiencyClass,
                    ServiceChargeEur = s.Details == null ? null : s.Details.ServiceChargeEur,
                    CommissionPercent = s.CommissionPercent
                })
                .ToListAsync(ct);
    }

    public async Task<List<ArchivedOffer>> GetArchivedOffersAsync(CancellationToken ct = default)
    {
        // Per flat no longer in FlatsSnapshots: its earliest and latest run and its first-seen time. Runs never overlap,
        // so the latest run is also the one that scraped it last.
        var latest = _dbContext.FlatsArchive
            .Where(a => !_dbContext.FlatsSnapshots.Any(s => s.Source == a.Source && s.ListingId == a.ListingId))
            .GroupBy(a => new { a.Source, a.ListingId })
            .Select(g => new
            {
                g.Key.Source,
                g.Key.ListingId,
                RunId = g.Max(a => a.ScrapingHistoryId),
                FirstRunId = g.Min(a => a.ScrapingHistoryId),
                FirstSeenUtc = g.Min(a => EF.Property<DateTime>(a, "CreatedAtUtc")),
            });

        var offers = await (
                from flat in latest
                join a in _dbContext.FlatsArchive
                    on new { flat.Source, flat.ListingId, ScrapingHistoryId = flat.RunId }
                    equals new { a.Source, a.ListingId, a.ScrapingHistoryId }
                join first in _dbContext.FlatsArchive
                    on new { flat.Source, flat.ListingId, ScrapingHistoryId = flat.FirstRunId }
                    equals new { first.Source, first.ListingId, first.ScrapingHistoryId }
                orderby a.ScrapedAtUtc descending
                select new ArchivedOffer
                {
                    District = a.District,
                    RoomsCount = a.RoomsCount,
                    PriceEur = a.PriceEur,
                    OriginalPriceEur = a.OriginalPriceEur,
                    PricePerMeter = a.PricePerMeter,
                    LivingAreaSqm = a.LivingAreaSqm,
                    Url = a.Url,
                    Source = a.Source,
                    ListingId = a.ListingId,
                    HasPreview = _dbContext.FlatPreviewsArchive.Any(p => p.Source == a.Source && p.ListingId == a.ListingId),
                    Availability = a.Details == null ? null : a.Details.Availability,
                    EnergyEfficiencyClass = a.Details == null ? null : a.Details.EnergyEfficiencyClass,
                    ServiceChargeEur = a.Details == null ? null : a.Details.ServiceChargeEur,
                    CommissionPercent = a.CommissionPercent,
                    FirstSeenUtc = flat.FirstSeenUtc,
                    LastSeenUtc = a.ScrapedAtUtc,
                    FirstPriceEur = first.PriceEur,
                })
            .ToListAsync(ct);

        // An aggregate bypasses the column's converter: the dates are UTC, so the API writes them with "Z"
        foreach (var offer in offers)
        {
            offer.FirstSeenUtc = DateTime.SpecifyKind(offer.FirstSeenUtc, DateTimeKind.Utc);
            offer.LastSeenUtc = DateTime.SpecifyKind(offer.LastSeenUtc, DateTimeKind.Utc);
        }
        return offers;
    }
}
