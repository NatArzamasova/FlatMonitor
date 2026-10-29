namespace FlatMonitor.Api.Services;

using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Cleans up after the previous instance of the service, before the first scrape job runs:
/// <list type="number">
/// <item>Jobs exist only in memory, so a run still "Running" at startup was started by an earlier instance that was
/// stopped without finishing it (e.g. the process was killed). It is marked as canceled.</item>
/// <item>If the last run failed or was canceled, its data is not kept: FlatsSnapshots is restored from the backup that
/// run made before it started (if there is one), as a canceled or failed run does itself when it can.</item>
/// </list>
/// A partially failed run is kept, as the running service keeps it: its aggregation was computed from that data,
/// so restoring the backup would make flats and aggregations disagree.
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

        var lastRun = (await _history.GetLatestAsync(ct)).Latest;
        if (lastRun is null || lastRun.Status is ScrapingStatus.Successful or ScrapingStatus.PartialFailed)
            return;

        if (!await _snapshots.HasBackupAsync(ct))
        {
            _logger.LogWarning("The last scrape run {RunId} ended as {Status}, but there is no backup to restore",
                lastRun.Id, lastRun.Status);
            return;
        }

        await _snapshots.RestoreFlatsSnapshotsAsync(ct);
        _logger.LogWarning("Restored FlatsSnapshots from the backup: the last scrape run {RunId} ended as {Status}",
            lastRun.Id, lastRun.Status);
    }
}
