using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping;
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
    public async Task AddFlatMonitorCore_ResolvesScrapeExecutionService()
    {
        // Async scope: ScraperCoordinator is only IAsyncDisposable, like in Program and ScrapeBackgroundService
        await using var scope = _provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

        act.Should().NotThrow();
    }
}
