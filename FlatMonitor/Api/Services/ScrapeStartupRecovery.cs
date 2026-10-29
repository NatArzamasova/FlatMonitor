namespace FlatMonitor.Api.Services;

using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Cleans up after the previous instance of the service, before the first scrape job runs.
/// Jobs exist only in memory, so a run still "Running" at startup was started by an earlier instance that was
/// stopped without finishing it (e.g. the process was killed). It is marked as canceled, and what it left in the
/// staging table is dropped. FlatsSnapshots needs nothing: a run only ever replaces whole sources that finished.
/// </summary>
public sealed class ScrapeStartupRecovery
{
    public const string InterruptedRunMessage = "Interrupted: the service stopped before the scrape finished";

    private readonly IScrapingHistoryRepository _history;
    private readonly ISnapshotRepository _snapshots;
    private readonly ILogger<ScrapeStartupRecovery> _logger;

    public ScrapeStartupRecovery(
        IScrapingHistoryRepository history,
        ISnapshotRepository snapshots,
        ILogger<ScrapeStartupRecovery> logger)
    {
        _history = history;
        _snapshots = snapshots;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var canceled = await _history.CancelUnfinishedRunsAsync(InterruptedRunMessage, ct);
        if (canceled > 0)
            _logger.LogWarning("Marked {Count} scrape run(s) left running by a previous instance as canceled", canceled);

        await _snapshots.DiscardRunAsync(ct);
    }
}
