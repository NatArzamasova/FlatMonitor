namespace FlatMonitor.Tests.Scraping.Common;

using FlatMonitor.Scraping.Common;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class GermanNumberParserTests
{
    [TestCase("1.250.000 €", 1250000)]
    [TestCase("€ 270.000", 270000)]
    [TestCase("84,5 m²", 84.5)]
    [TestCase("3,5 Zimmer", 3.5)]
    [TestCase("2 Zimmer", 2)]
    [TestCase("1.250,5", 1250.5)]
    [TestCase("17,90 m2", 17.9)]
    [TestCase("104", 104)]
    [TestCase("104.5", 104.5)]
    [TestCase("3,57% inkl. MwSt.", 3.57)]
    [TestCase("3%", 3)]
    [TestCase("1234.567", 1234.567)]
    public void ParseDecimal_ReadsTheGermanNumber(string text, double expected)
    {
        GermanNumberParser.ParseDecimal(text).Should().Be(expected);
    }

    [Test]
    public void ParseDecimal_TakesTheFirstNumberOfTheText()
    {
        GermanNumberParser.ParseDecimal("Baujahr 1976, saniert 2010").Should().Be(1976);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Preis auf Anfrage")]
    public void ParseDecimal_WithoutNumber_ReturnsNull(string? text)
    {
        GermanNumberParser.ParseDecimal(text).Should().BeNull();
    }

    [TestCase("1.250.000 €", 1250000)]
    [TestCase("270.000,50 €", 270000)]
    [TestCase("Hausgeld 250,90 €", 250)]
    [TestCase("1976", 1976)]
    public void ParseInteger_DropsTheDecimalPlaces(string text, int expected)
    {
        GermanNumberParser.ParseInteger(text).Should().Be(expected);
    }

    [Test]
    public void ParseInteger_AboveIntRange_ReturnsNull()
    {
        GermanNumberParser.ParseInteger("99.999.999.999 €").Should().BeNull();
    }

    [TestCase("1.250", 1250)]
    [TestCase("74,5", 74.5)]
    [TestCase("74.5", 74.5)]
    [TestCase("3", 3)]
    public void ToDouble_ConvertsOneNumber(string number, double expected)
    {
        GermanNumberParser.ToDouble(number).Should().Be(expected);
    }
}
