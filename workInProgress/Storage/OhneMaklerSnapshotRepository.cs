namespace workInProgress.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using workInProgress.Data;
using workInProgress.Models;

public sealed class OhneMaklerSnapshotRepository : ISnapshotRepository
{
    private readonly ListingsDbContext _dbContext;
    private readonly bool _ownsContext;

    public OhneMaklerSnapshotRepository(DbContextOptions<ListingsDbContext>? options = null)
    {
        if (options != null)
        {
            _dbContext = new ListingsDbContext(options);
            _ownsContext = true;
        }
        else
        {
            var defaultOptions = new DbContextOptionsBuilder<ListingsDbContext>()
                .UseSqlite(GetDefaultConnectionString())
                .Options;
            _dbContext = new ListingsDbContext(defaultOptions);
            _ownsContext = true;
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // For in-memory databases (tests), use EnsureCreatedAsync.
        // For SQLite (production), use MigrateAsync to apply migrations while preserving data.
        var provider = _dbContext.Database.ProviderName;
        if (provider?.Contains("InMemory") ?? false)
        {
            await _dbContext.Database.EnsureCreatedAsync(ct);
        }
        else
        {
            await _dbContext.Database.MigrateAsync(ct);
        }
    }

    public async Task UpsertListingsAsync(List<Flat> listings, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.ToString("O");
        var listingIds = listings.Select(l => l.ListingId).ToList();

        var existingSnapshots = await _dbContext.OhneMaklerSnapshots
            .Where(s => listingIds.Contains(s.ListingId))
            .ToListAsync(ct);

        var existingMap = existingSnapshots.ToDictionary(s => s.ListingId);

        // Build a map of createdAt values before removing
        var createdAtMap = existingMap.ToDictionary(
            kvp => kvp.Key,
            kvp => _dbContext.Entry(kvp.Value).Property<string>("CreatedAtUtc").CurrentValue ?? now);

        // Remove all existing
        foreach (var existing in existingSnapshots)
        {
            _dbContext.OhneMaklerSnapshots.Remove(existing);
        }

        // Add/update all listings
        foreach (var listing in listings)
        {
            var snapshot = new OhneMaklerSnapshot
            {
                ListingId = listing.ListingId,
                Url = listing.Url,
                Title = listing.Title,
                Address = listing.Address,
                PostalCode = listing.PostalCode,
                District = listing.District,
                PriceEur = listing.PriceEur,
                RoomsCount = listing.RoomsCount,
                LivingAreaSqm = listing.LivingAreaSqm,
                Details = listing.Details,
                ScrapedAtUtc = listing.ScrapedAtUtc,
            };

            var createdAt = createdAtMap.TryGetValue(listing.ListingId, out var existing) ? existing : now;

            _dbContext.Entry(snapshot).Property<string>("CreatedAtUtc").CurrentValue = createdAt;
            _dbContext.Entry(snapshot).Property<string>("UpdatedAtUtc").CurrentValue = now;
            _dbContext.OhneMaklerSnapshots.Add(snapshot);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsContext)
            await _dbContext.DisposeAsync();
    }

    private static string GetDefaultConnectionString()
    {
        var dataDir = StoragePaths.GetDefaultDataDirectory();
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "listings.db");
        return $"Data Source={dbPath}";
    }
}
