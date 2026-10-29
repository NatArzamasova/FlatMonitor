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
            .Where(h => h.Status == ScrapingStatus.Successful)
            .OrderByDescending(h => h.StartTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new ScrapingStatusHistory(latest, latestSuccessful);
    }

    public Task<int> CancelUnfinishedRunsAsync(string reason, CancellationToken cancellationToken = default) =>
        _dbContext.ScrapingHistories
            .Where(h => h.Status == ScrapingStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(h => h.Status, ScrapingStatus.Canceled)
                .SetProperty(h => h.ErrorMessage, reason), cancellationToken);

    public async Task<int> StartAsync(CancellationToken cancellationToken = default)
    {
        var run = new ScrapingHistory { StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Running };
        _dbContext.ScrapingHistories.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Later writes go through ExecuteUpdate; a tracked copy would only go stale
        _dbContext.Entry(run).State = EntityState.Detached;
        return run.Id;
    }

    // ExecuteUpdate writes only this row, never other pending changes of the shared context (e.g. from a failed batch)
    public Task AddProgressAsync(int runId, int flats, CancellationToken cancellationToken = default) =>
        _dbContext.ScrapingHistories
            .Where(h => h.Id == runId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(h => h.FlatsScrapedCount, h => h.FlatsScrapedCount + flats), cancellationToken);

    public Task CompleteAsync(int runId, ScrapingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default) =>
        _dbContext.ScrapingHistories
            .Where(h => h.Id == runId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(h => h.EndTimeUtc, DateTime.UtcNow)
                .SetProperty(h => h.Status, status)
                .SetProperty(h => h.ErrorMessage, errorMessage), cancellationToken);
}
