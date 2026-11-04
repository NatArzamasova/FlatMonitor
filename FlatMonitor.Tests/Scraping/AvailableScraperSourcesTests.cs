namespace FlatMonitor.Tests.Scraping;

using FlatMonitor.Scraping;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class AvailableScraperSourcesTests
{
    private static readonly AvailableScraperSources Sources = new(["OhneMakler", "ImmobilienScout24", "Immowelt"]);

    [Test]
    public void Select_WithoutAList_SelectsAll()
    {
        Sources.Select(null).Should().Equal("OhneMakler", "ImmobilienScout24", "Immowelt");
    }

    [Test]
    public void Select_TakesTheNamedSourcesInAnyCaseInTheOrderOfTheAvailableOnes()
    {
        Sources.Select(["immobilienscout24", " OhneMakler ", "ImmobilienScout24"])
            .Should().Equal("OhneMakler", "ImmobilienScout24");
    }

    [Test]
    public void Select_WithAnEmptyList_Fails()
    {
        var act = () => Sources.Select([]);

        act.Should().Throw<ArgumentException>().WithMessage("*at least one source*");
    }

    [Test]
    public void Select_WithASourceThatIsNotAvailable_FailsAndListsTheAvailableOnes()
    {
        var act = () => new AvailableScraperSources(["OhneMakler"]).Select(["Immowelt"]);

        act.Should().Throw<ArgumentException>().WithMessage("*'Immowelt'*the sources are OhneMakler.");
    }
}
