namespace FlatMonitor.Api.Services;

using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

public class ScrapeExecutionService
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFlatAggregationRepository _aggregationRepository;
    private readonly ScraperCoordinator _coordinator;
    private readonly ILogger<ScrapeExecutionService> _logger;

    public ScrapeExecutionService(
        ISnapshotRepository snapshotRepository,
        IFlatAggregationRepository aggregationRepository,
        ScraperCoordinator coordinator,
        ILogger<ScrapeExecutionService> logger)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _aggregationRepository = aggregationRepository ?? throw new ArgumentNullException(nameof(aggregationRepository));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Backs up and clears FlatsSnapshots, scrapes all sources, and refreshes the aggregations.
    /// If anything throws (including cancellation), the backup is restored before the exception propagates.
    /// A run where only some sources failed returns normally and keeps the backup.
    /// </summary>
    public async Task<ScrapeSummary> ExecuteAsync(
        Func<int, Task>? onBatchSaved = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Backing up FlatsSnapshots table");
        await _snapshotRepository.BackupFlatsSnapshotsAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Clearing FlatsSnapshots table");
            await _snapshotRepository.ClearAsync(cancellationToken);

            var summary = await _coordinator.ScrapeAllSourcesAsync(onBatchSaved, cancellationToken);

            _logger.LogInformation("Refreshing flat aggregations");
            await _aggregationRepository.RefreshAsync(cancellationToken);

            if (summary.Success)
            {
                _logger.LogInformation("Scrape successful, deleting backup");
                await _snapshotRepository.DeleteBackupAsync(cancellationToken);
            }

            return summary;
        }
        catch
        {
            await TryRestoreBackupAsync();
            throw;
        }
    }

    /// <summary>Restores FlatsSnapshots; a failing restore is logged so the scrape's own exception still propagates.</summary>
    private async Task TryRestoreBackupAsync()
    {
        try
        {
            _logger.LogWarning("Scrape did not finish, restoring FlatsSnapshots from backup");
            await _snapshotRepository.RestoreFlatsSnapshotsAsync(CancellationToken.None);
        }
        catch (Exception restoreEx)
        {
            _logger.LogError(restoreEx, "Failed to restore FlatsSnapshots from backup");
        }
    }
}
