using FlatMonitor.Models;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Models;

[TestFixture]
public class KarlsruheDistrictsTests
{
    [Test]
    public void All_ContainsTheTrackedDistricts()
    {
        KarlsruheDistricts.All.Should().HaveCount(27);
        KarlsruheDistricts.All.Should().Contain(["Durlach", "Innenstadt-West", "Rüppurr"]);
    }

    [Test]
    public void All_IsCaseInsensitive()
    {
        KarlsruheDistricts.All.Contains("durlach").Should().BeTrue();
    }

    [Test]
    public void ToCanonical_ReturnsNameForExactMatch()
    {
        KarlsruheDistricts.ToCanonical("Durlach").Should().Be("Durlach");
    }

    [TestCase("durlach")]
    [TestCase("DURLACH")]
    [TestCase("  Durlach ")]
    public void ToCanonical_ReturnsCanonicalSpellingIgnoringCaseAndWhitespace(string name)
    {
        KarlsruheDistricts.ToCanonical(name).Should().Be("Durlach");
    }

    [TestCase("Bad Schönborn")]
    [TestCase("KA-Oberreut")]
    [TestCase("Ober")]
    public void ToCanonical_ReturnsNullForUnknownOrPartialName(string name)
    {
        KarlsruheDistricts.ToCanonical(name).Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ToCanonical_ReturnsNullForBlankName(string? name)
    {
        KarlsruheDistricts.ToCanonical(name).Should().BeNull();
    }

    [TestCase("Oberreut", "Oberreut")]
    [TestCase("oberreut", "Oberreut")]
    [TestCase(" Durlach ", "Durlach")]
    [TestCase("KA-Oberreut", "Oberreut")]
    [TestCase("ka-innenstadt-west", "Innenstadt-West")]
    [TestCase("Karlsruhe (Rüppurr)", "Rüppurr")]
    public void Match_ReturnsCanonicalDistrictForExactOrContainedName(string scraped, string expected)
    {
        KarlsruheDistricts.Match(scraped).Should().Be(expected);
    }

    // Weststadt is contained in both names; the longer, more specific district must win
    [TestCase("KA-Nordweststadt", "Nordweststadt")]
    [TestCase("Karlsruhe-Südweststadt", "Südweststadt")]
    [TestCase("Südweststadt", "Südweststadt")]
    [TestCase("KA-Weststadt", "Weststadt")]
    public void Match_PrefersTheLongestContainedDistrict(string scraped, string expected)
    {
        KarlsruheDistricts.Match(scraped).Should().Be(expected);
    }

    [TestCase("Bad Schönborn")]
    [TestCase("Ober")]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Match_ReturnsNullForUntrackedOrBlankName(string? scraped)
    {
        KarlsruheDistricts.Match(scraped).Should().BeNull();
    }
}
