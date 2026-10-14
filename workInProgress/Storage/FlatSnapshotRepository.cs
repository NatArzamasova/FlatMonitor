namespace workInProgress.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using workInProgress.Data;
using workInProgress.Models;

public sealed class FlatSnapshotRepository : ISnapshotRepository
{
    private readonly ListingsDbContext _dbContext;
    private readonly bool _ownsContext;

    public FlatSnapshotRepository(DbContextOptions<ListingsDbContext>? options = null)
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

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database save failed: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
            throw;
        }
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
