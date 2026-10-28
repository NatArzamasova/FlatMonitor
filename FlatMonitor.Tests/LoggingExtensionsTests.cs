using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlatMonitor.Tests;

[TestFixture]
public class LoggingExtensionsTests
{
    private ServiceProvider _provider = null!;
    private ILoggerFactory _loggerFactory = null!;

    [SetUp]
    public void Setup()
    {
        _provider = new ServiceCollection()
            .AddLogging(logging => logging.AddFlatMonitorLogging())
            .BuildServiceProvider();
        _loggerFactory = _provider.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [TestCase("FlatMonitor.Api.Services.ScrapeExecutionService")]
    [TestCase("FlatMonitor.Api.Services.ScrapeBackgroundService")]
    public void AddFlatMonitorLogging_OwnCategories_LogFromInformation(string category)
    {
        var logger = _loggerFactory.CreateLogger(category);

        logger.IsEnabled(LogLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogLevel.Debug).Should().BeFalse();
    }

    [TestCase("Microsoft.EntityFrameworkCore.Database.Command")]
    [TestCase("Microsoft.AspNetCore.Hosting.Diagnostics")]
    [TestCase("System.Net.Http.HttpClient.Default.LogicalHandler")]
    public void AddFlatMonitorLogging_FrameworkCategories_LogFromWarning(string category)
    {
        var logger = _loggerFactory.CreateLogger(category);

        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }
}
