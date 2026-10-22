namespace FlatMonitor.Storage;

using FlatMonitor.Api.Models;



public interface IScrapingHistoryRepository
{
    Task<ScrapingStatusHistory> GetLatestAsync(CancellationToken cancellationToken = default);
}

public sealed record ScrapingStatusHistory(
    ScrapingHistoryDto? Latest,
    ScrapingHistoryDto? LatestSuccessful);
