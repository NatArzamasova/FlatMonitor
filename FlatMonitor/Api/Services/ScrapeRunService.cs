namespace FlatMonitor.Api.Services;

using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>How a scrape run ended; <see cref="Summary"/> is set when the scrape itself finished.</summary>
public sealed record ScrapeRunResult(ScrapingStatus Status, ScrapeSummary? Summary = null, string? ErrorMessage = null);

/// <summary>
/// One scrape run with its ScrapingHistories row: records the start, the progress per saved batch and the end state
/// (Successful, PartialFailed, Failed or Canceled). Used by the API's background service and the <c>scrape</c> command,
/// so both leave the same history and startup recovery sees an interrupted run of either.
/// </summary>
public sealed class ScrapeRunService
{
    private readonly IScrapingHistoryRepository _history;
    private readonly ScrapeExecutionService _execution;
    private readonly ILogger<ScrapeRunService> _logger;

    public ScrapeRunService(IScrapingHistoryRepository history, ScrapeExecutionService execution, ILogger<ScrapeRunService> logger)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs the scrape. Never throws for a failed or canceled scrape: the result says how it ended. Canceled
    /// before the history row was saved, nothing ran and nothing is recorded.
    /// </summary>
    public async Task<ScrapeRunResult> RunAsync(CancellationToken ct = default)
    {
        int runId;
        try
        {
            runId = await _history.StartAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Scrape canceled before it started");
            return new ScrapeRunResult(ScrapingStatus.Canceled);
        }

        _logger.LogInformation("Scrape run {RunId} started", runId);
        try
        {
            var summary = await _execution.ExecuteAsync(flats => _history.AddProgressAsync(runId, flats, ct), ct);

            if (summary.Success)
            {
                _logger.LogInformation("Scrape run {RunId} completed successfully with {FlatsCount} flats", runId, summary.TotalFlats);
                return await CompleteAsync(runId, new ScrapeRunResult(ScrapingStatus.Successful, summary));
            }

            var errors = string.Join("; ", summary.Errors);
            _logger.LogWarning("Scrape run {RunId} completed with errors: {Errors}", runId, errors);
            return await CompleteAsync(runId, new ScrapeRunResult(ScrapingStatus.PartialFailed, summary, errors));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Scrape run {RunId} was canceled", runId);
            return await CompleteAsync(runId, new ScrapeRunResult(ScrapingStatus.Canceled));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scrape run {RunId} failed", runId);
            return await CompleteAsync(runId, new ScrapeRunResult(ScrapingStatus.Failed, ErrorMessage: ex.Message));
        }
    }

    /// <summary>Saves the end state even if the run was canceled.</summary>
    private async Task<ScrapeRunResult> CompleteAsync(int runId, ScrapeRunResult result)
    {
        await _history.CompleteAsync(runId, result.Status, result.ErrorMessage, CancellationToken.None);
        return result;
    }
}
