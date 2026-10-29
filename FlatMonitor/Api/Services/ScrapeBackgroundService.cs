namespace FlatMonitor.Api.Services;

using FlatMonitor.Models;
using FlatMonitor.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static FlatMonitor.Api.Services.ScrapeJobService;

/// <summary>Runs queued scrape jobs one at a time and records each run in ScrapingHistories.</summary>
public class ScrapeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScrapeJobService _jobService;
    private readonly ILogger<ScrapeBackgroundService> _logger;

    public ScrapeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ScrapeJobService jobService,
        ILogger<ScrapeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobService = jobService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Before the first job, so a new run cannot be mistaken for a leftover
        await RecoverFromPreviousInstanceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            ScrapeJob job;
            try
            {
                job = await _jobService.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ScrapeBackgroundService");
            }
        }
    }

    /// <summary>See <see cref="ScrapeStartupRecovery"/>.</summary>
    private async Task RecoverFromPreviousInstanceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ScrapeStartupRecovery>().RunAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not worth blocking the job queue for: the next scrape empties the staging table anyway
            _logger.LogError(ex, "Could not clean up after the previous instance of the service");
        }
    }

    private async Task ProcessJobAsync(ScrapeJob job, CancellationToken stoppingToken)
    {
        if (_jobService.StartJob(job, stoppingToken) is not { } jobCt)
        {
            _logger.LogInformation("Scrape job {JobId} was canceled before it started", job.JobId);
            return;
        }

        var finalStatus = ScrapeJobStatus.Failed;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FlatsDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

            _logger.LogInformation("Starting scrape job {JobId}", job.JobId);
            var history = new ScrapingHistory { StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Running };
            dbContext.ScrapingHistories.Add(history);
            await dbContext.SaveChangesAsync(jobCt);

            try
            {
                // A failed or canceled run leaves FlatsSnapshots consistent: it only replaces sources that finished
                var result = await executionService.ExecuteAsync(async batchCount =>
                {
                    history.FlatsScrapedCount += batchCount;
                    dbContext.ScrapingHistories.Update(history);
                    await dbContext.SaveChangesAsync(jobCt);
                    _logger.LogInformation("Job {JobId}: Batch saved: +{BatchCount} flats. Total: {Total}",
                        job.JobId, batchCount, history.FlatsScrapedCount);
                }, jobCt);

                if (result.Success)
                {
                    _logger.LogInformation("Scrape job {JobId} completed successfully with {FlatsCount} flats",
                        job.JobId, result.TotalFlats);
                    await CompleteHistoryAsync(dbContext, history, ScrapingStatus.Successful);
                }
                else
                {
                    var errorSummary = string.Join("; ", result.Errors);
                    _logger.LogWarning("Scrape job {JobId} completed with errors: {Errors}", job.JobId, errorSummary);
                    await CompleteHistoryAsync(dbContext, history, ScrapingStatus.PartialFailed, errorSummary);
                }

                finalStatus = ScrapeJobStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Scrape job {JobId} was canceled", job.JobId);
                await CompleteHistoryAsync(dbContext, history, ScrapingStatus.Canceled);
                finalStatus = ScrapeJobStatus.Canceled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape job {JobId} failed", job.JobId);
                await CompleteHistoryAsync(dbContext, history, ScrapingStatus.Failed, ex.Message);
            }
        }
        catch (OperationCanceledException) when (jobCt.IsCancellationRequested)
        {
            // Canceled before the history row was saved: nothing was scraped, so there is no run to record
            _logger.LogWarning("Scrape job {JobId} was canceled before it started scraping", job.JobId);
            finalStatus = ScrapeJobStatus.Canceled;
        }
        finally
        {
            _jobService.FinishJob(job.JobId, finalStatus);
        }
    }

    /// <summary>
    /// Saves the history row's end state, even if the job was canceled. The repositories share this scoped
    /// context, so flats or aggregations from an interrupted write may still be tracked: drop them first,
    /// then Update re-attaches the history entity.
    /// </summary>
    private static async Task CompleteHistoryAsync(FlatsDbContext dbContext, ScrapingHistory history, ScrapingStatus status, string? errorMessage = null)
    {
        dbContext.ChangeTracker.Clear();
        history.EndTimeUtc = DateTime.UtcNow;
        history.Status = status;
        history.ErrorMessage = errorMessage;
        dbContext.ScrapingHistories.Update(history);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }
}
