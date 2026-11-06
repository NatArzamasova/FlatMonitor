namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class ImmoweltOptionsTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void FromConfiguration_WithoutSection_UsesTheDefaults()
    {
        var options = ImmoweltOptions.FromConfiguration(Configuration([]));

        options.Should().Be(new ImmoweltOptions
        {
            CdpUrl = "http://127.0.0.1:9222",
            LaunchChrome = true,
            ChromePath = null,
            ChromeUserDataDir = @"%LOCALAPPDATA%\FlatMonitorChrome",
            SearchUrl = ImmoweltOptions.DefaultSearchUrl,
            PauseBetweenPagesMs = 5_000,
            MaxPauseBetweenPagesMs = 10_000,
            RobotCheckWaitSeconds = 600,
            SaveDebugPages = false,
        });
    }

    [Test]
    public void DefaultSearchUrl_SearchesFlatsToBuyWithoutAuctions()
    {
        ImmoweltOptions.DefaultSearchUrl.Should().Contain("distributionTypes=Buy&")
            .And.Contain("estateTypes=Apartment")
            .And.NotContain("Auction");
    }

    [Test]
    public void FromConfiguration_ReadsEveryValueOfTheSection()
    {
        var options = ImmoweltOptions.FromConfiguration(Configuration(new()
        {
            ["Scrapers:Immowelt:CdpUrl"] = "http://127.0.0.1:9333",
            ["Scrapers:Immowelt:LaunchChrome"] = "false",
            ["Scrapers:Immowelt:ChromePath"] = @"D:\Portable\chrome.exe",
            ["Scrapers:Immowelt:ChromeUserDataDir"] = @"D:\Profiles\Scraper",
            ["Scrapers:Immowelt:SearchUrl"] = "https://example.org/search",
            ["Scrapers:Immowelt:PauseBetweenPagesMs"] = "0",
            ["Scrapers:Immowelt:MaxPauseBetweenPagesMs"] = "2000",
            ["Scrapers:Immowelt:RobotCheckWaitSeconds"] = "120",
            ["Scrapers:Immowelt:SaveDebugPages"] = "True",
        }));

        options.Should().Be(new ImmoweltOptions
        {
            CdpUrl = "http://127.0.0.1:9333",
            LaunchChrome = false,
            ChromePath = @"D:\Portable\chrome.exe",
            ChromeUserDataDir = @"D:\Profiles\Scraper",
            SearchUrl = "https://example.org/search",
            PauseBetweenPagesMs = 0,
            MaxPauseBetweenPagesMs = 2_000,
            RobotCheckWaitSeconds = 120,
            SaveDebugPages = true,
        });
    }

    [TestCase("CdpUrl")]
    [TestCase("LaunchChrome")]
    [TestCase("ChromePath")]
    [TestCase("ChromeUserDataDir")]
    [TestCase("SearchUrl")]
    [TestCase("PauseBetweenPagesMs")]
    [TestCase("MaxPauseBetweenPagesMs")]
    [TestCase("RobotCheckWaitSeconds")]
    [TestCase("SaveDebugPages")]
    public void FromConfiguration_WithAnEmptyValue_UsesTheDefault(string key)
    {
        var options = ImmoweltOptions.FromConfiguration(Configuration(new()
        {
            [$"Scrapers:Immowelt:{key}"] = " ",
        }));

        options.Should().Be(new ImmoweltOptions());
    }

    [TestCase("PauseBetweenPagesMs", "-1")]
    [TestCase("PauseBetweenPagesMs", "5s")]
    [TestCase("PauseBetweenPagesMs", "2.5")]
    [TestCase("MaxPauseBetweenPagesMs", "-1")]
    [TestCase("RobotCheckWaitSeconds", "-1")]
    [TestCase("SaveDebugPages", "yes")]
    [TestCase("LaunchChrome", "ja")]
    public void FromConfiguration_WithAnInvalidValue_FailsWithTheSettingsName(string key, string value)
    {
        var act = () => ImmoweltOptions.FromConfiguration(Configuration(new()
        {
            [$"Scrapers:Immowelt:{key}"] = value,
        }));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*Scrapers:Immowelt:{key}*'{value}'*");
    }

    [Test]
    public void FromConfiguration_WithAMaximumPauseBelowTheMinimum_FailsWithBothSettings()
    {
        var act = () => ImmoweltOptions.FromConfiguration(Configuration(new()
        {
            ["Scrapers:Immowelt:PauseBetweenPagesMs"] = "8000",
            ["Scrapers:Immowelt:MaxPauseBetweenPagesMs"] = "6000",
        }));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxPauseBetweenPagesMs' (6000)*'Scrapers:Immowelt:PauseBetweenPagesMs' (8000)*");
    }

    [Test]
    public void NextPause_ByDefault_IsFrom5To10SecondsAndVaries()
    {
        var options = new ImmoweltOptions();
        var random = new Random(42);

        var pauses = Enumerable.Range(0, 1_000).Select(_ => options.NextPause(random)).ToList();

        pauses.Should().OnlyContain(p => p >= TimeSpan.FromSeconds(5) && p <= TimeSpan.FromSeconds(10));
        pauses.Should().OnlyContain(p => p.Ticks % TimeSpan.TicksPerMillisecond == 0, "whole milliseconds");
        pauses.Min().Should().BeLessThan(TimeSpan.FromSeconds(5.5));
        pauses.Max().Should().BeGreaterThan(TimeSpan.FromSeconds(9.5));
    }

    [TestCase(3_000, 3_000, 3_000)]
    [TestCase(0, 0, 0)]
    [TestCase(4_000, 1_000, 4_000)]
    public void NextPause_WithoutARange_IsTheMinimum(int min, int max, int expectedMs)
    {
        var options = new ImmoweltOptions { PauseBetweenPagesMs = min, MaxPauseBetweenPagesMs = max };

        options.NextPause().Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }
}
