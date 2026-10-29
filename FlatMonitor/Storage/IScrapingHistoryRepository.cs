namespace FlatMonitor.Storage;

using FlatMonitor.Models;

public interface IScrapingHistoryRepository
{
    Task<ScrapingStatusHistory> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every run still <see cref="ScrapingStatus.Running"/> as <see cref="ScrapingStatus.Canceled"/> with
    /// <paramref name="reason"/> as error message; the end time stays empty because it is unknown. Returns the number of runs.
    /// </summary>
    Task<int> CancelUnfinishedRunsAsync(string reason, CancellationToken cancellationToken = default);
}

public sealed record ScrapingStatusHistory(
    ScrapingHistory? Latest,
    ScrapingHistory? LatestSuccessful);
