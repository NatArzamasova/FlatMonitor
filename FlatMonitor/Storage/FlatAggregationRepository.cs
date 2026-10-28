namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;


public sealed class FlatAggregationRepository : IFlatAggregationRepository
{
    private readonly FlatsDbContext _dbContext;
    private readonly bool _ownsContext;

    public FlatAggregationRepository(DbContextOptions<FlatsDbContext> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dbContext = new FlatsDbContext(options);
        _ownsContext = true;
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

    public Task<List<FlatHistory>> GetHistoryAsync(CancellationToken ct = default) =>
        _dbContext.FlatAggregations
            .AsNoTracking()
            .OrderBy(a => a.ScrapingDate)
            .ThenBy(a => a.District)
            .ThenBy(a => a.RoomsCount)
            .Select(a => new FlatHistory
            {
                ScrapingDate = a.ScrapingDate.ToString("yyyy-MM-dd"),
                District = a.District,
                RoomsCount = a.RoomsCount,
                AvgPricePerMeter = a.AvgPricePerMeter,
                Count = a.Count
            })
            .ToListAsync(ct);

    public async Task<List<FlatAggregation>> GetLatestAsync(CancellationToken ct = default)
    {
        var latestScrapingDate = await _dbContext.FlatAggregations
            .MaxAsync(a => (DateOnly?)a.ScrapingDate, ct);
        if (latestScrapingDate is null)
            return [];

        return await _dbContext.FlatAggregations
            .AsNoTracking()
            .Where(a => a.ScrapingDate == latestScrapingDate)
            .OrderBy(a => a.District)
            .ThenBy(a => a.RoomsCount)
            .Select(a => new FlatAggregation
            {
                PostalCode = null,
                District = a.District,
                RoomsCount = a.RoomsCount,
                Availability = null,
                Count = a.Count,
                AvgPricePerMeter = a.AvgPricePerMeter
            })
            .ToListAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsContext)
            await _dbContext.DisposeAsync();
    }
}
