namespace FlatMonitor.Tests.Scraping;

using FlatMonitor.Scraping;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class ScraperSourcesTests
{
    private static IConfiguration Configuration(string? sources) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Scrapers:Sources"] = sources }).Build();

    [Test]
    public void All_AreTheSourcesInScrapeOrder()
    {
        ScraperSources.All.Should().Equal("OhneMakler", "ImmobilienScout24", "Immowelt");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void FromConfiguration_WithoutSources_SelectsAll(string? sources)
    {
        ScraperSources.FromConfiguration(Configuration(sources)).Should().BeEquivalentTo(ScraperSources.All);
    }

    [Test]
    public void FromConfiguration_WithoutKey_SelectsAll()
    {
        ScraperSources.FromConfiguration(new ConfigurationBuilder().Build()).Should().BeEquivalentTo(ScraperSources.All);
    }

    [TestCase("Immowelt", new[] { "Immowelt" })]
    [TestCase("immowelt", new[] { "Immowelt" }, TestName = "FromConfiguration_IgnoresTheCase")]
    [TestCase(" OhneMakler , IMMOWELT ", new[] { "OhneMakler", "Immowelt" }, TestName = "FromConfiguration_TakesACommaSeparatedList")]
    [TestCase("Immowelt,Immowelt", new[] { "Immowelt" }, TestName = "FromConfiguration_IgnoresDuplicates")]
    public void FromConfiguration_SelectsTheNamedSources(string sources, string[] expected)
    {
        ScraperSources.FromConfiguration(Configuration(sources)).Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Parse_UnknownSource_FailsAndListsTheSources()
    {
        var act = () => ScraperSources.Parse("Immowelt,immowlt");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Scrapers:Sources*'immowlt'*OhneMakler, ImmobilienScout24, Immowelt*");
    }
}
