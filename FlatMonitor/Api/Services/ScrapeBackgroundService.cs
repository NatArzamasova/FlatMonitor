namespace workInProgress.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using workInProgress.Data;
using workInProgress.Models;
using workInProgress.Scraping;

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

        using var scope = _serviceProvider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ScraperCoordinator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlatsDbContext>();

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

            await dbContext.FlatsSnapshots.ExecuteDeleteAsync(jobCt);
            await dbContext.SaveChangesAsync(jobCt);

            coordinator.SetOnBatchSavedCallback(async (batchCount) =>
            {
                history.FlatsScrapedCount += batchCount;
                dbContext.ScrapingHistories.Update(history);
                await dbContext.SaveChangesAsync(jobCt);
                _logger.LogInformation("Job {JobId}: Batch saved: +{BatchCount} flats. Total: {Total}",
                    job.JobId, batchCount, history.FlatsScrapedCount);
            });

            var flatsCount = await coordinator.ScrapeAllSourcesAsync(jobCt);

            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Successful";
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(jobCt);

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

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Canceled);
            _logger.LogInformation("Scrape job {JobId} was canceled", job.JobId);
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            history.EndTimeUtc = endTime;
            history.Status = "Failed";
            history.ErrorMessage = ex.Message;
            dbContext.ScrapingHistories.Update(history);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _jobService.UpdateJobStatus(job.JobId, ScrapeJobService.ScrapeJobStatus.Failed);
            _logger.LogError(ex, "Scrape job {JobId} failed", job.JobId);
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
