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
        var now = DateTime.UtcNow.ToString("O");

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
                // Update existing entity in-place
                existing.Url = listing.Url;
                existing.Address = listing.Address;
                existing.PostalCode = listing.PostalCode;
                existing.District = listing.District;
                existing.PriceEur = listing.PriceEur;
                existing.OriginalPriceEur = listing.OriginalPriceEur;
                existing.PricePerMeter = CalculatePricePerMeter(listing.PriceEur, listing.LivingAreaSqm);
                existing.RoomsCount = listing.RoomsCount;
                existing.LivingAreaSqm = listing.LivingAreaSqm;
                existing.Details = listing.Details;
                existing.ScrapedAtUtc = listing.ScrapedAtUtc;

                var entry = _dbContext.Entry(existing);
                entry.Property<string>("UpdatedAtUtc").CurrentValue = now;
            }
            else
            {
                // Add new entity
                var snapshot = new FlatSnapshot
                {
                    Source = listing.Source,
                    ListingId = listing.ListingId,
                    Url = listing.Url,
                    Address = listing.Address,
                    PostalCode = listing.PostalCode,
                    District = listing.District,
                    PriceEur = listing.PriceEur,
                    OriginalPriceEur = listing.OriginalPriceEur,
                    PricePerMeter = CalculatePricePerMeter(listing.PriceEur, listing.LivingAreaSqm),
                    RoomsCount = listing.RoomsCount,
                    LivingAreaSqm = listing.LivingAreaSqm,
                    Details = listing.Details,
                    ScrapedAtUtc = listing.ScrapedAtUtc,
                };

                var entry = _dbContext.Entry(snapshot);
                entry.Property<string>("CreatedAtUtc").CurrentValue = now;
                entry.Property<string>("UpdatedAtUtc").CurrentValue = now;
                _dbContext.FlatsSnapshots.Add(snapshot);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private static int? CalculatePricePerMeter(int? priceEur, double? livingAreaSqm)
    {
        if (!priceEur.HasValue || !livingAreaSqm.HasValue || livingAreaSqm == 0)
            return null;

        return (int)Math.Round((double)priceEur.Value / livingAreaSqm.Value);
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
