using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests;

/// <summary>Checks FlatMonitor/appsettings.json, which is linked into the test output.</summary>
[TestFixture]
public class AppSettingsTests
{
    private IConfiguration _configuration = null!;

    [SetUp]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    [Test]
    public void Storage_KeepsDatabaseAndDataInProjectDataDirectory()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "FlatMonitor");

        var storage = StorageOptions.FromConfiguration(_configuration, projectDir);

        storage.DataDirectory.Should().Be(Path.Combine(projectDir, "data"));
        storage.ConnectionString.Should().Be($"Data Source={Path.Combine(projectDir, "data", "listings.db")}");
    }

    [TestCase("FlatMonitor.Api.Services.ScrapeExecutionService")]
    [TestCase("FlatMonitor.Api.Services.ScrapeBackgroundService")]
    public void Logging_OwnCategories_LogFromInformation(string category)
    {
        using var provider = CreateLoggingProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(category);

        logger.IsEnabled(LogLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogLevel.Debug).Should().BeFalse();
    }

    [TestCase("Microsoft.EntityFrameworkCore.Database.Command")]
    [TestCase("Microsoft.AspNetCore.Hosting.Diagnostics")]
    [TestCase("System.Net.Http.HttpClient.Default.LogicalHandler")]
    public void Logging_FrameworkCategories_LogFromWarning(string category)
    {
        using var provider = CreateLoggingProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(category);

        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }

    // Mirrors the host builders, which apply the "Logging" section before Program adds the console provider
    private ServiceProvider CreateLoggingProvider() =>
        new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.AddConfiguration(_configuration.GetSection("Logging"));
                logging.AddFlatMonitorLogging();
            })
            .BuildServiceProvider();
}
