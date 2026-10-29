namespace FlatMonitor.Api.Services;

using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

public class ScrapeExecutionService
{
    private readonly IFlatAggregationRepository _aggregationRepository;
    private readonly ScraperCoordinator _coordinator;
    private readonly ILogger<ScrapeExecutionService> _logger;

    public ScrapeExecutionService(
        IFlatAggregationRepository aggregationRepository,
        ScraperCoordinator coordinator,
        ILogger<ScrapeExecutionService> logger)
    {
        _aggregationRepository = aggregationRepository ?? throw new ArgumentNullException(nameof(aggregationRepository));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scrapes all sources and refreshes the aggregations. FlatsSnapshots is never cleared: each source that
    /// succeeds replaces its rows, a failed source keeps the previous ones (see <see cref="ScraperCoordinator"/>).
    /// So a run that fails, is canceled or only partly succeeds leaves consistent data and needs nothing restored.
    /// </summary>
    public async Task<ScrapeSummary> ExecuteAsync(
        Func<int, Task>? onBatchSaved = null,
        CancellationToken cancellationToken = default)
    {
        ScrapeSummary summary;
        try
        {
            summary = await _coordinator.ScrapeAllSourcesAsync(onBatchSaved, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Sources published before the cancel stay published: keep the aggregations in line with them
            await TryRefreshAggregationsAfterCancelAsync();
            throw;
        }

        _logger.LogInformation("Refreshing flat aggregations");
        await _aggregationRepository.RefreshAsync(cancellationToken);

        return summary;
    }

    /// <summary>A failing refresh is logged, so the cancellation still propagates.</summary>
    private async Task TryRefreshAggregationsAfterCancelAsync()
    {
        try
        {
            _logger.LogInformation("Scrape canceled, refreshing flat aggregations for the sources published before");
            await _aggregationRepository.RefreshAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh flat aggregations after the scrape was canceled");
        }
    }
}
