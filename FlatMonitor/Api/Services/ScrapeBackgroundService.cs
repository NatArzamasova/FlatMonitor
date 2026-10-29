namespace FlatMonitor.Api.Services;

using FlatMonitor.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static FlatMonitor.Api.Services.ScrapeJobService;

/// <summary>Runs queued scrape jobs one at a time; each job is one <see cref="ScrapeRunService"/> run.</summary>
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
            _logger.LogInformation("Starting scrape job {JobId}", job.JobId);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ScrapeRunService>().RunAsync(jobCt);
            finalStatus = result.Status switch
            {
                ScrapingStatus.Successful or ScrapingStatus.PartialFailed => ScrapeJobStatus.Completed,
                ScrapingStatus.Canceled => ScrapeJobStatus.Canceled,
                _ => ScrapeJobStatus.Failed,
            };
        }
        finally
        {
            _jobService.FinishJob(job.JobId, finalStatus);
        }
    }
}
