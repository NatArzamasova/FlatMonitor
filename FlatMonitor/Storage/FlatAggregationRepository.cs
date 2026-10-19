namespace workInProgress.Storage;

using Microsoft.EntityFrameworkCore;
using workInProgress.Data;

public sealed class FlatAggregationRepository : IFlatAggregationRepository
{
    private readonly FlatsDbContext _dbContext;
    private readonly bool _ownsContext;

    public FlatAggregationRepository(DbContextOptions<FlatsDbContext>? options = null)
    {
        if (options != null)
        {
            _dbContext = new FlatsDbContext(options);
            _ownsContext = true;
        }
        else
        {
            var defaultOptions = new DbContextOptionsBuilder<FlatsDbContext>()
                .UseSqlite(GetDefaultConnectionString())
                .Options;
            _dbContext = new FlatsDbContext(defaultOptions);
            _ownsContext = true;
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var provider = _dbContext.Database.ProviderName;
        if (provider?.Contains("InMemory") ?? false)
            await _dbContext.Database.EnsureCreatedAsync(ct);
        else
            await _dbContext.Database.MigrateAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var snapshots = await _dbContext.FlatsSnapshots.AsNoTracking().ToListAsync(ct);
        var aggregated = FlatAggregationCalculator.Compute(snapshots);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Set the ScrapingDate for all aggregations
        foreach (var agg in aggregated)
        {
            agg.ScrapingDate = today;
        }

        // Remove existing aggregations for today
        var existingForToday = await _dbContext.FlatAggregations
            .Where(a => a.ScrapingDate == today)
            .ToListAsync(ct);
        _dbContext.FlatAggregations.RemoveRange(existingForToday);

        // Add the new aggregations
        await _dbContext.FlatAggregations.AddRangeAsync(aggregated, ct);
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
