using System.Runtime.CompilerServices;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FlatMonitor.Tests.Scraping.Common;
using static FlatMonitor.Api.Services.ScrapeJobService;

namespace FlatMonitor.Tests.Api.Services;

/// <summary>
/// Runs real scrape jobs through the hosted service, with the production storage classes on in-memory SQLite.
/// The FlatsSnapshots table starts with one flat ("existing"), so a restored backup is visible.
/// </summary>
[TestFixture]
public class ScrapeBackgroundServiceTests
{
    private SqliteConnection _connection = null!;
    private string _dataDir = null!;
    private ScrapeJobService _jobService = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dataDir = Path.Combine(Path.GetTempPath(), $"ScrapeBackgroundServiceTests_{Guid.NewGuid()}");
        _jobService = new ScrapeJobService();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await new FlatSnapshotRepository(dbContext).UpsertListingsAsync([FakeFlatScraper.CreateFlat("existing")]);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public async Task Startup_MarksRunsLeftRunningAsCanceled_WithoutTouchingTheNewJob()
    {
        // Arrange: a run an earlier instance never finished
        await using (var dbContext = CreateDbContext())
        {
            dbContext.ScrapingHistories.Add(new ScrapingHistory
            {
                StartTimeUtc = DateTime.UtcNow.AddHours(-1), Status = ScrapingStatus.Running, FlatsScrapedCount = 121,
            });
            await dbContext.SaveChangesAsync();
        }

        // Act: start the service with a new job queued
        var job = await RunJobAsync(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] });

        // Assert
        job.Status.Should().Be(ScrapeJobStatus.Completed);
        await using var readContext = CreateDbContext();
        var runs = await readContext.ScrapingHistories.OrderBy(h => h.Id).ToListAsync();
        runs.Should().HaveCount(2);
        runs[0].Status.Should().Be(ScrapingStatus.Canceled);
        runs[0].ErrorMessage.Should().Be(ScrapeStartupRecovery.InterruptedRunMessage);
        runs[0].EndTimeUtc.Should().BeNull();
        runs[1].Status.Should().Be(ScrapingStatus.Successful);
    }

    [Test]
    public async Task ProcessJob_WhenAllSourcesSucceed_SavesSuccessfulHistoryWithFlatCount()
    {
        // Act
        var job = await RunJobAsync(new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1"), FakeFlatScraper.CreateFlat("2")] });

        // Assert
        job.Status.Should().Be(ScrapeJobStatus.Completed);
        await using var dbContext = CreateDbContext();
        var history = await dbContext.ScrapingHistories.SingleAsync();
        history.Status.Should().Be(ScrapingStatus.Successful);
        history.FlatsScrapedCount.Should().Be(2);
        history.EndTimeUtc.Should().NotBeNull();
        (await dbContext.FlatsSnapshots.Select(s => s.ListingId).ToListAsync()).Should().BeEquivalentTo("1", "2");
    }

    [Test]
    public async Task ProcessJob_WhenASourceFails_SavesPartialFailedHistoryWithTheError()
    {
        // Act
        var job = await RunJobAsync(
            new FakeFlatScraper("OhneMakler") { Flats = [FakeFlatScraper.CreateFlat("1")] },
            new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") });

        // Assert
        job.Status.Should().Be(ScrapeJobStatus.Completed);
        await using var dbContext = CreateDbContext();
        var history = await dbContext.ScrapingHistories.SingleAsync();
        history.Status.Should().Be(ScrapingStatus.PartialFailed);
        history.ErrorMessage.Should().Be("ImmobilienScout24: Login failed");
    }

    [Test]
    public async Task ProcessJob_WhenCanceled_SavesCanceledHistoryAndRestoresTheBackup()
    {
        // Arrange
        var scraper = new BlockingScraper();

        // Act
        var job = await RunJobAsync([scraper], whileRunning: async () =>
        {
            await scraper.Started.WaitAsync(TimeSpan.FromSeconds(10));
            _jobService.CancelCurrentJob().Should().BeTrue();
        });

        // Assert
        job.Status.Should().Be(ScrapeJobStatus.Canceled);
        await using var dbContext = CreateDbContext();
        (await dbContext.ScrapingHistories.SingleAsync()).Status.Should().Be(ScrapingStatus.Canceled);
        (await dbContext.FlatsSnapshots.SingleAsync()).ListingId.Should().Be("existing");
    }

    [Test]
    public async Task ProcessJob_WhenScrapeFailsWithUnsavedChanges_SavesFailedHistoryWithoutThem()
    {
        // Act: the aggregation refresh adds a row to the shared context, then fails before saving it
        var job = await RunJobAsync(
            [new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] }],
            aggregations: sp => new InterruptedAggregationRepository(sp.GetRequiredService<FlatsDbContext>()));

        // Assert
        job.Status.Should().Be(ScrapeJobStatus.Failed);
        await using var dbContext = CreateDbContext();
        var history = await dbContext.ScrapingHistories.SingleAsync();
        history.Status.Should().Be(ScrapingStatus.Failed);
        history.ErrorMessage.Should().Be(InterruptedAggregationRepository.ErrorMessage);
        (await dbContext.FlatAggregations.CountAsync()).Should().Be(0);
        (await dbContext.FlatsSnapshots.SingleAsync()).ListingId.Should().Be("existing");
    }

    [Test]
    public async Task ProcessJob_CanceledBeforeTheHistoryRowIsSaved_EndsAsCanceled()
    {
        // Act: the job is canceled while its history row is being saved
        var job = await RunJobAsync(
            [new FakeFlatScraper { Flats = [FakeFlatScraper.CreateFlat("1")] }],
            interceptor: new CancelJobOnFirstSaveInterceptor(_jobService));

        // Assert: canceled, not failed; nothing was scraped or recorded
        job.Status.Should().Be(ScrapeJobStatus.Canceled);
        await using var dbContext = CreateDbContext();
        (await dbContext.ScrapingHistories.AnyAsync()).Should().BeFalse();
        (await dbContext.FlatsSnapshots.SingleAsync()).ListingId.Should().Be("existing");
    }

    [Test]
    public async Task ProcessJob_JobCanceledBeforeItStarted_IsSkipped()
    {
        // Arrange
        _jobService.TryQueueJob(out var job).Should().BeTrue();
        _jobService.CancelCurrentJob();
        var service = CreateService(BuildProvider([new FakeFlatScraper()]));

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _jobService.GetJobStatus(job!.JobId)!.Status.Should().Be(ScrapeJobStatus.Canceled);
        await using var dbContext = CreateDbContext();
        (await dbContext.ScrapingHistories.AnyAsync()).Should().BeFalse();
    }

    private Task<ScrapeJob> RunJobAsync(params IScraper[] scrapers) => RunJobAsync(scrapers, aggregations: null);

    /// <summary>Queues one job, runs the hosted service until the job has finished, and returns its final state.</summary>
    private async Task<ScrapeJob> RunJobAsync(
        IScraper[] scrapers,
        Func<IServiceProvider, IFlatAggregationRepository>? aggregations = null,
        Func<Task>? whileRunning = null,
        IInterceptor? interceptor = null)
    {
        await using var provider = BuildProvider(scrapers, aggregations, interceptor);
        _jobService.TryQueueJob(out var job).Should().BeTrue();
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        if (whileRunning != null)
            await whileRunning();
        await WaitUntilFinishedAsync(job!.JobId);
        await service.StopAsync(CancellationToken.None);

        return _jobService.GetJobStatus(job.JobId)!;
    }

    private ServiceProvider BuildProvider(
        IScraper[] scrapers,
        Func<IServiceProvider, IFlatAggregationRepository>? aggregations = null,
        IInterceptor? interceptor = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<FlatsDbContext>(o =>
        {
            o.UseSqlite(_connection);
            if (interceptor != null)
                o.AddInterceptors(interceptor);
        });
        services.AddScoped<ISnapshotRepository, FlatSnapshotRepository>();
        if (aggregations != null)
            services.AddScoped(aggregations);
        else
            services.AddScoped<IFlatAggregationRepository, FlatAggregationRepository>();
        services.AddScoped(sp => new ScraperCoordinator(
            scrapers, sp.GetRequiredService<ISnapshotRepository>(), new SnapshotWriter(), _dataDir,
            NullLogger<ScraperCoordinator>.Instance));
        services.AddScoped<ScrapeExecutionService>();
        services.AddScoped<IScrapingHistoryRepository, ScrapingHistoryRepository>();
        services.AddScoped<ScrapeStartupRecovery>();
        return services.BuildServiceProvider();
    }

    private ScrapeBackgroundService CreateService(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), _jobService, NullLogger<ScrapeBackgroundService>.Instance);

    private FlatsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);

    private async Task WaitUntilFinishedAsync(Guid jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_jobService.GetJobStatus(jobId)!.Status is ScrapeJobStatus.Queued or ScrapeJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Scrape job did not finish in time");
            await Task.Delay(20);
        }
    }

    /// <summary>Signals <see cref="Started"/>, then waits until the scrape is canceled.</summary>
    private sealed class BlockingScraper : IScraper
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Source => "Blocking";
        public Task Started => _started.Task;

        public async IAsyncEnumerable<Flat> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            _started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }
    }

    /// <summary>Cancels the running job when the first save (the history row) starts, so that save is canceled.</summary>
    private sealed class CancelJobOnFirstSaveInterceptor(ScrapeJobService jobService) : SaveChangesInterceptor
    {
        private bool _canceled;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_canceled)
            {
                _canceled = true;
                jobService.CancelCurrentJob();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Adds an aggregation to the shared context, then fails before saving it.</summary>
    private sealed class InterruptedAggregationRepository(FlatsDbContext dbContext) : IFlatAggregationRepository
    {
        public const string ErrorMessage = "Refresh interrupted";

        public Task RefreshAsync(CancellationToken ct = default)
        {
            dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
            {
                ScrapingDate = DateOnly.FromDateTime(DateTime.UtcNow),
                District = "Innenstadt",
                RoomsCount = 2,
                Count = 1,
                AvgPricePerMeter = 5000m,
            });
            throw new InvalidOperationException(ErrorMessage);
        }

        public Task<List<FlatHistory>> GetHistoryAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<List<FlatAggregation>> GetLatestAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
