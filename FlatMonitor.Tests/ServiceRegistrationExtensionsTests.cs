using NUnit.Framework;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests;

[TestFixture]
public class ServiceRegistrationExtensionsTests
{
    private string _contentRoot = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void Setup()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"ServiceRegistrationTests_{Guid.NewGuid()}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [StorageOptions.DataDirectoryKey] = "data",
                [$"ConnectionStrings:{StorageOptions.ConnectionStringName}"] = "Data Source=data/test.db",
            })
            .Build();

        var services = new ServiceCollection().AddLogging();
        services.AddFlatMonitorCore(configuration, _contentRoot);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools(); // pooled connections keep the test database file open
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    [Test]
    public void AddFlatMonitorCore_RegistersStorageResolvedAgainstContentRoot()
    {
        var storage = _provider.GetRequiredService<StorageOptions>();

        storage.DataDirectory.Should().Be(Path.Combine(_contentRoot, "data"));
        storage.ConnectionString.Should().Contain(Path.Combine(_contentRoot, "data", "test.db"));
    }

    [Test]
    public async Task AddFlatMonitorCore_RegistersScrapersInOrder()
    {
        await using var scope = _provider.CreateAsyncScope();

        var scrapers = scope.ServiceProvider.GetServices<IScraper>();

        scrapers.Select(s => s.Source).Should().Equal("OhneMakler", "ImmobilienScout24");
    }

    [Test]
    public void AddFlatMonitorCore_RegistersImmobilienScout24OptionsFromConfiguration()
    {
        // The test configuration has no Scrapers section
        _provider.GetRequiredService<ImmobilienScout24Options>().Should().Be(new ImmobilienScout24Options());
    }

    [Test]
    public async Task AddFlatMonitorCore_ResolvesScrapeExecutionService()
    {
        // Async scope, like in Program and ScrapeBackgroundService
        await using var scope = _provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

        act.Should().NotThrow();
    }

    [Test]
    public async Task AddFlatMonitorCore_RepositoriesUseTheScopedDbContext()
    {
        // Arrange
        _provider.GetRequiredService<StorageOptions>().EnsureDirectoriesExist();
        await using var scope = _provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlatsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<ISnapshotRepository>();
        await repository.UpsertListingsAsync([new Flat
        {
            ListingId = "1",
            Url = "/immobilie/1/",
            Title = "Listing 1",
            Address = "Address 1",
            ScrapedAtUtc = DateTime.UtcNow,
            Source = "OhneMakler",
        }]);

        // Act: a transaction on the scoped context only covers the repository if both use that context
        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            await repository.ClearAsync();
            await transaction.RollbackAsync();
        }

        // Assert
        (await dbContext.FlatsSnapshots.CountAsync()).Should().Be(1);
    }
}
