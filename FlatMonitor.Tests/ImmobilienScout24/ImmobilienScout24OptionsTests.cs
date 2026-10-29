namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Scraping.ImmobilienScout24;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24OptionsTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void FromConfiguration_WithoutSection_UsesTheDefaults()
    {
        var options = ImmobilienScout24Options.FromConfiguration(Configuration([]));

        options.Should().Be(new ImmobilienScout24Options
        {
            Headless = false,
            SearchUrl = ImmobilienScout24Options.DefaultSearchUrl,
            SaveDebugPages = false,
        });
    }

    [Test]
    public void FromConfiguration_ReadsEveryValueOfTheSection()
    {
        var options = ImmobilienScout24Options.FromConfiguration(Configuration(new()
        {
            ["Scrapers:ImmobilienScout24:Headless"] = "true",
            ["Scrapers:ImmobilienScout24:SearchUrl"] = "https://example.org/search",
            ["Scrapers:ImmobilienScout24:SaveDebugPages"] = "True",
        }));

        options.Headless.Should().BeTrue();
        options.SearchUrl.Should().Be("https://example.org/search");
        options.SaveDebugPages.Should().BeTrue();
    }

    [Test]
    public void FromConfiguration_WithEmptySearchUrl_UsesTheDefault()
    {
        var options = ImmobilienScout24Options.FromConfiguration(Configuration(new()
        {
            ["Scrapers:ImmobilienScout24:SearchUrl"] = " ",
        }));

        options.SearchUrl.Should().Be(ImmobilienScout24Options.DefaultSearchUrl);
    }

    [Test]
    public void FromConfiguration_WithAnInvalidFlag_FailsWithTheSettingsName()
    {
        var act = () => ImmobilienScout24Options.FromConfiguration(Configuration(new()
        {
            ["Scrapers:ImmobilienScout24:Headless"] = "yes",
        }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Scrapers:ImmobilienScout24:Headless*'yes'*");
    }
}
