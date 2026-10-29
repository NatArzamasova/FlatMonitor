namespace FlatMonitor.Api.Services;

using FlatMonitor.Models;
using FlatMonitor.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ScrapeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScrapeJobService _jobService;
    private readonly ScrapeStateService _stateService;
    private readonly ILogger<ScrapeBackgroundService> _logger;
    private CancellationTokenSource? _currentJobCts;
    private Guid? _currentJobId;

    public ScrapeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ScrapeJobService jobService,
        ScrapeStateService stateService,
        ILogger<ScrapeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobService = jobService;
        _stateService = stateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_jobService.TryDequeueJob(out var job))
                {
                    await ProcessJobAsync(job, stoppingToken);
                }
                else
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ScrapeBackgroundService");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(ScrapeJobService.ScrapeJob job, CancellationToken stoppingToken)
    {
        if (job == null) return;

        _currentJobId = job.JobId;
        _currentJobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var jobCt = _currentJobCts.Token;

        _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Running);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlatsDbContext>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<ISnapshotRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

        var startTime = DateTime.UtcNow;
        var history = new ScrapingHistory
        {
            StartTimeUtc = startTime,
            Status = "Running"
        };

        try
        {
            _logger.LogInformation("Starting scrape job {JobId}", job.JobId);

            dbContext.ScrapingHistories.Add(history);
            await dbContext.SaveChangesAsync(jobCt);

            var result = await executionService.ExecuteAsync(async (batchCount) =>
            {
                history.FlatsScrapedCount += batchCount;
                dbContext.ScrapingHistories.Update(history);
                await dbContext.SaveChangesAsync(jobCt);
                _logger.LogInformation("Job {JobId}: Batch saved: +{BatchCount} flats. Total: {Total}",
                    job.JobId, batchCount, history.FlatsScrapedCount);
            }, jobCt);

            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;

            if (result.Success)
            {
                history.Status = "Successful";
                _logger.LogInformation("Scrape job {JobId} completed successfully with {FlatsCount} flats",
                    job.JobId, result.TotalFlats);
            }
            else
            {
                history.Status = "PartialFailed";
                var errorSummary = string.Join("; ", result.Errors);
                history.ErrorMessage = errorSummary;
                _logger.LogWarning("Scrape job {JobId} completed with errors: {Errors}", job.JobId, errorSummary);
            }

            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(jobCt);

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Canceled";
            DiscardPendingChanges(dbContext);
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogWarning("Scrape job {JobId} was canceled, restoring from backup", job.JobId);
            try
            {
                await snapshotRepo.RestoreFlatsSnapshotsAsync(CancellationToken.None);
            }
            catch (Exception restoreEx)
            {
                _logger.LogError(restoreEx, "Failed to restore backup for job {JobId}", job.JobId);
            }

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Canceled);
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Failed";
            history.ErrorMessage = ex.Message;
            DiscardPendingChanges(dbContext);
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex, "Scrape job {JobId} failed, restoring from backup", job.JobId);
            try
            {
                await snapshotRepo.RestoreFlatsSnapshotsAsync(CancellationToken.None);
            }
            catch (Exception restoreEx)
            {
                _logger.LogError(restoreEx, "Failed to restore backup for job {JobId}", job.JobId);
            }

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Failed);
        }
        finally
        {
            _currentJobCts?.Dispose();
            _currentJobCts = null;
            _currentJobId = null;
        }
    }

    /// <summary>
    /// The repositories share this scoped context. Flats from an interrupted upsert are still tracked,
    /// so drop them before saving the history row; Update re-attaches the history entity.
    /// </summary>
    private static void DiscardPendingChanges(FlatsDbContext dbContext) => dbContext.ChangeTracker.Clear();

    public void CancelCurrentJob()
    {
        if (_currentJobCts != null && !_currentJobCts.IsCancellationRequested)
        {
            _currentJobCts.Cancel();
            _logger.LogInformation("Cancellation requested for job {JobId}", _currentJobId);
        }
    }
}
