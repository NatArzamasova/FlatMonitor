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
}
