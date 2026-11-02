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
            SearchUrl = ImmoweltOptions.DefaultSearchUrl,
            PauseBetweenPagesMs = 5_000,
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
            ["Scrapers:Immowelt:SearchUrl"] = "https://example.org/search",
            ["Scrapers:Immowelt:PauseBetweenPagesMs"] = "0",
            ["Scrapers:Immowelt:SaveDebugPages"] = "True",
        }));

        options.Should().Be(new ImmoweltOptions
        {
            CdpUrl = "http://127.0.0.1:9333",
            SearchUrl = "https://example.org/search",
            PauseBetweenPagesMs = 0,
            SaveDebugPages = true,
        });
    }

    [TestCase("CdpUrl")]
    [TestCase("SearchUrl")]
    [TestCase("PauseBetweenPagesMs")]
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
    [TestCase("SaveDebugPages", "yes")]
    public void FromConfiguration_WithAnInvalidValue_FailsWithTheSettingsName(string key, string value)
    {
        var act = () => ImmoweltOptions.FromConfiguration(Configuration(new()
        {
            [$"Scrapers:Immowelt:{key}"] = value,
        }));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*Scrapers:Immowelt:{key}*'{value}'*");
    }
}
