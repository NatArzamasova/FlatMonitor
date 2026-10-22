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
            .Select(h => new ScrapingHistory
            {
                Id = h.Id,
                StartTimeUtc = h.StartTimeUtc,
                EndTimeUtc = h.EndTimeUtc,
                Status = h.Status,
                ErrorMessage = h.ErrorMessage,
                FlatsScrapedCount = h.FlatsScrapedCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        var latestSuccessful = await _dbContext.ScrapingHistories
            .AsNoTracking()
            .Where(h => h.Status == "Successful")
            .OrderByDescending(h => h.StartTimeUtc)
            .Select(h => new ScrapingHistory
            {
                Id = h.Id,
                StartTimeUtc = h.StartTimeUtc,
                EndTimeUtc = h.EndTimeUtc,
                Status = h.Status,
                ErrorMessage = h.ErrorMessage,
                FlatsScrapedCount = h.FlatsScrapedCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ScrapingStatusHistory(latest, latestSuccessful);
    }
}
