namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;

public sealed class ScrapingHistoryRepository : IScrapingHistoryRepository
{
    private readonly FlatsDbContext _dbContext;

    public ScrapingHistoryRepository(FlatsDbContext dbContext) => _dbContext = dbContext;

    public async Task<ScrapingStatusHistory> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _dbContext.ScrapingHistories
            .AsNoTracking()
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var latestSuccessful = await _dbContext.ScrapingHistories
            .AsNoTracking()
            .Where(h => h.Status == "Successful")
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new ScrapingStatusHistory(latest, latestSuccessful);
    }
}
