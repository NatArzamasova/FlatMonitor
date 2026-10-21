namespace workInProgress.Storage;

using workInProgress.Api.Models;

public interface IScrapingHistoryRepository
{
    Task<ScrapingStatusHistory> GetLatestAsync(CancellationToken cancellationToken = default);
}

public sealed record ScrapingStatusHistory(
    ScrapingHistoryDto? Latest,
    ScrapingHistoryDto? LatestSuccessful);
