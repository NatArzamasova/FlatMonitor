namespace workInProgress.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using workInProgress.Data;
using workInProgress.Scraping;
using workInProgress.Storage;

public class ScrapeExecutionService
{
    private readonly FlatsDbContext _dbContext;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFlatAggregationRepository _aggregationRepository;
    private readonly ScraperCoordinator _coordinator;
    private readonly ILogger<ScrapeExecutionService> _logger;

    public ScrapeExecutionService(
        FlatsDbContext dbContext,
        ISnapshotRepository snapshotRepository,
        IFlatAggregationRepository aggregationRepository,
        ScraperCoordinator coordinator,
        ILogger<ScrapeExecutionService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _aggregationRepository = aggregationRepository ?? throw new ArgumentNullException(nameof(aggregationRepository));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScrapeResult> ExecuteAsync(
        Func<int, Task>? onBatchSaved = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Backing up FlatsSnapshots table");
        await _snapshotRepository.BackupFlatsSnapshotsAsync(cancellationToken);

        _logger.LogInformation("Clearing FlatsSnapshots table");
        await _dbContext.FlatsSnapshots.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (onBatchSaved != null)
        {
            _coordinator.SetOnBatchSavedCallback(onBatchSaved);
        }

        var flatsCount = await _coordinator.ScrapeAllSourcesAsync(cancellationToken);

        _logger.LogInformation("Refreshing flat aggregations");
        await _aggregationRepository.RefreshAsync(cancellationToken);

        var success = _coordinator.ScraperErrors.Count == 0;

        if (success)
        {
            _logger.LogInformation("Scrape successful, deleting backup");
            await _snapshotRepository.DeleteBackupAsync(cancellationToken);
        }

        return new ScrapeResult
        {
            Success = success,
            FlatsCount = flatsCount,
            ScraperErrors = new List<string>(_coordinator.ScraperErrors)
        };
    }
}

public class ScrapeResult
{
    public bool Success { get; set; }
    public int FlatsCount { get; set; }
    public List<string> ScraperErrors { get; set; } = new();
}
