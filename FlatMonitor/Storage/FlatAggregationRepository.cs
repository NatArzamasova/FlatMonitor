namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using FlatMonitor.Storage.ReadModels;
using Microsoft.EntityFrameworkCore;


public sealed class FlatAggregationRepository : IFlatAggregationRepository
{
    private readonly FlatsDbContext _dbContext;

    public FlatAggregationRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

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
                ScrapingDate = a.ScrapingDate,
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
                District = a.District,
                RoomsCount = a.RoomsCount,
                Count = a.Count,
                AvgPricePerMeter = a.AvgPricePerMeter
            })
            .ToListAsync(ct);
    }
}
