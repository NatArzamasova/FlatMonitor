namespace workInProgress.Storage;

using Microsoft.EntityFrameworkCore;
using workInProgress.Data;
using workInProgress.Models;

public sealed class ListingsRepository : IAsyncDisposable
{
    private readonly ListingsDbContext _dbContext;
    private readonly bool _ownsContext;

    public ListingsRepository(DbContextOptions<ListingsDbContext>? options = null)
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
        await _dbContext.Database.EnsureCreatedAsync(ct);
    }

    public async Task UpsertListingsAsync(List<Listing> listings, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.ToString("O");
        var listingIds = listings.Select(l => l.ListingId).ToList();

        var existingListings = await _dbContext.Listings
            .Where(l => listingIds.Contains(l.ListingId))
            .ToListAsync(ct);

        var existingMap = existingListings.ToDictionary(l => l.ListingId);

        // Build a map of createdAt values before removing
        var createdAtMap = existingMap.ToDictionary(
            kvp => kvp.Key,
            kvp => _dbContext.Entry(kvp.Value).Property<string>("CreatedAtUtc").CurrentValue ?? now);

        // Remove all existing
        foreach (var existing in existingListings)
        {
            _dbContext.Listings.Remove(existing);
        }

        // Add/update all listings
        foreach (var listing in listings)
        {
            var createdAt = createdAtMap.TryGetValue(listing.ListingId, out var existing) ? existing : now;

            _dbContext.Entry(listing).Property<string>("CreatedAtUtc").CurrentValue = createdAt;
            _dbContext.Entry(listing).Property<string>("UpdatedAtUtc").CurrentValue = now;
            _dbContext.Listings.Add(listing);
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
        var dataDir = Path.Combine("data");
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "listings.db");
        return $"Data Source={dbPath}";
    }
}
