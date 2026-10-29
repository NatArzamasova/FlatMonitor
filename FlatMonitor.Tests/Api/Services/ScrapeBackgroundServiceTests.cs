using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Api.Services;

[TestFixture]
public class ScrapeBackgroundServiceTests
{
    /// <summary>
    /// Tests that backup/restore methods are available and can be mocked.
    /// Full integration testing of ScrapeBackgroundService is covered by
    /// the FlatSnapshotRepositoryTests.BackupRestoreDelete_FullWorkflow_* tests.
    /// </summary>
    [Test]
    public async Task BackupRestoreMethods_AreCallable_WithoutError()
    {
        // Arrange
        var mockRepository = new Mock<ISnapshotRepository>();

        mockRepository
            .Setup(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockRepository
            .Setup(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockRepository
            .Setup(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - All methods should be callable
        await mockRepository.Object.BackupFlatsSnapshotsAsync();
        await mockRepository.Object.RestoreFlatsSnapshotsAsync();
        await mockRepository.Object.DeleteBackupAsync();

        // Assert - Verify they were called
        mockRepository.Verify(r => r.BackupFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.RestoreFlatsSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.DeleteBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessJob_WhenScrapeFailsWithUnsavedChanges_SavesFailedHistoryWithoutThem()
    {
        // Arrange: repositories share the scoped context, like in production
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<FlatsDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<ISnapshotRepository, FlatSnapshotRepository>();
        services.AddScoped<IFlatAggregationRepository, InterruptedAggregationRepository>();
        services.AddScoped(sp => new ScraperCoordinator(
            [], sp.GetRequiredService<ISnapshotRepository>(), new SnapshotWriter(), Path.GetTempPath()));
        services.AddScoped<ScrapeExecutionService>();
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<FlatsDbContext>().Database.EnsureCreatedAsync();

        var jobService = new ScrapeJobService();
        var job = jobService.QueueJob();
        var backgroundService = new ScrapeBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            jobService,
            new ScrapeStateService(),
            NullLogger<ScrapeBackgroundService>.Instance);

        // Act
        await backgroundService.StartAsync(CancellationToken.None);
        await WaitUntilFinishedAsync(jobService, job.JobId);
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        jobService.GetJobStatus(job.JobId)!.Status.Should().Be(ScrapeJobService.ScrapeJobStatus.Failed);
        await using var dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(connection).Options);
        var history = await dbContext.ScrapingHistories.SingleAsync();
        history.Status.Should().Be("Failed");
        history.ErrorMessage.Should().Be(InterruptedAggregationRepository.ErrorMessage);
        (await dbContext.FlatAggregations.CountAsync()).Should().Be(0);
    }

    private static async Task WaitUntilFinishedAsync(ScrapeJobService jobService, Guid jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (jobService.GetJobStatus(jobId)!.Status is ScrapeJobService.ScrapeJobStatus.Queued or ScrapeJobService.ScrapeJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Scrape job did not finish in time");
            await Task.Delay(20);
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
