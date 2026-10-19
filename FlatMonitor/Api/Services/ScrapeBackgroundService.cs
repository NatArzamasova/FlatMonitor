namespace workInProgress.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Scraping;
using workInProgress.Storage;

public class ScrapeBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ScrapeJobService _jobService;
    private readonly ScrapeStateService _stateService;
    private readonly ILogger<ScrapeBackgroundService> _logger;
    private CancellationTokenSource? _currentJobCts;
    private Guid? _currentJobId;

    public ScrapeBackgroundService(
        IServiceProvider serviceProvider,
        ScrapeJobService jobService,
        ScrapeStateService stateService,
        ILogger<ScrapeBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
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

        await using var scope = _serviceProvider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ScraperCoordinator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlatsDbContext>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<ISnapshotRepository>();
        var aggregationRepo = scope.ServiceProvider.GetRequiredService<IFlatAggregationRepository>();

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

            // Backup before clearing
            _logger.LogInformation("Backing up FlatsSnapshots table");
            await snapshotRepo.BackupFlatsSnapshotsAsync(jobCt);

            await dbContext.FlatsSnapshots.ExecuteDeleteAsync(jobCt);
            await dbContext.SaveChangesAsync(jobCt);
            _logger.LogInformation("Cleared FlatsSnapshots table");

            coordinator.SetOnBatchSavedCallback(async (batchCount) =>
            {
                history.FlatsScrapedCount += batchCount;
                dbContext.ScrapingHistories.Update(history);
                await dbContext.SaveChangesAsync(jobCt);
                _logger.LogInformation("Job {JobId}: Batch saved: +{BatchCount} flats. Total: {Total}",
                    job.JobId, batchCount, history.FlatsScrapedCount);
            });

            var flatsCount = await coordinator.ScrapeAllSourcesAsync(jobCt);

            _logger.LogInformation("Refreshing flat aggregations for job {JobId}", job.JobId);
            await aggregationRepo.RefreshAsync(jobCt);

            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Successful";
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(jobCt);

            // Delete backup on success
            _logger.LogInformation("Scrape successful, deleting backup");
            await snapshotRepo.DeleteBackupAsync(jobCt);

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Completed);
            _logger.LogInformation("Scrape job {JobId} completed successfully with {FlatsCount} flats",
                job.JobId, flatsCount);
        }
        catch (OperationCanceledException)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Canceled";
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Restore from backup on cancellation
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
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Restore from backup on failure
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

    public void CancelCurrentJob()
    {
        if (_currentJobCts != null && !_currentJobCts.IsCancellationRequested)
        {
            _currentJobCts.Cancel();
            _logger.LogInformation("Cancellation requested for job {JobId}", _currentJobId);
        }
    }
}
