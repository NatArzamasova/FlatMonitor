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

    public async Task<ScrapeSummary> ExecuteAsync(
        Func<int, Task>? onBatchSaved = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Backing up FlatsSnapshots table");
        await _snapshotRepository.BackupFlatsSnapshotsAsync(cancellationToken);

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
}
