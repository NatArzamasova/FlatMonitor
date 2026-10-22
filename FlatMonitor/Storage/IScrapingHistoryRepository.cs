namespace FlatMonitor.Storage;

using FlatMonitor.Models;



public interface IScrapingHistoryRepository
{
    Task<ScrapingStatusHistory> GetLatestAsync(CancellationToken cancellationToken = default);
}

public sealed record ScrapingStatusHistory(
    ScrapingHistory? Latest,
    ScrapingHistory? LatestSuccessful);
