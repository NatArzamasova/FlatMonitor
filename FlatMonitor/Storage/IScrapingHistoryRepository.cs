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

    /// <summary>Records a new run as <see cref="ScrapingStatus.Running"/>, started now. Returns its id.</summary>
    Task<int> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds <paramref name="flats"/> to the run's scraped count.</summary>
    Task AddProgressAsync(int runId, int flats, CancellationToken cancellationToken = default);

    /// <summary>Records the run's end: now, with <paramref name="status"/> and <paramref name="errorMessage"/>.</summary>
    Task CompleteAsync(int runId, ScrapingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
}

public sealed record ScrapingStatusHistory(
    ScrapingHistory? Latest,
    ScrapingHistory? LatestSuccessful);
