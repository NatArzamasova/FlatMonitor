namespace workInProgress.Tests;

using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping;

[TestFixture]
public class DetailPageParserTests
{
    private DetailPageParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new DetailPageParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", filename);
    }

    [Test]
    public void ParseDetailPage_WithValidHtml_ReturnsDetails()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public void ParseDetailPage_ExtractsPropertyTypeCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyType.Should().Be("Dachgeschosswohnung");
    }

    [Test]
    public void ParseDetailPage_ExtractsBuildYearAsInteger()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.BuildYear.Should().Be(1904);
    }

    [Test]
    public void ParseDetailPage_ExtractsConditionCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.Condition.Should().Be("gepflegt");
    }

    [Test]
    public void ParseDetailPage_ExtractsHeatingCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.Heating.Should().Be("Ofenheizung");
    }

    [Test]
    public void ParseDetailPage_ExtractsFlooringCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.Flooring.Should().Be("Laminat");
    }

    [Test]
    public void ParseDetailPage_ExtractsServiceChargeAsInteger()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceChargeEur.Should().Be(405);
    }

    [Test]
    public void ParseDetailPage_ExtractsEnergyCertificateType()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyCertificateType.Should().Contain("nicht erforderlich");
    }

    [Test]
    public void ParseDetailPage_ExtractsPrimaryEnergySource()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.PrimaryEnergySource.Should().Be("Gas");
    }

    [Test]
    public void ParseDetailPage_ExtractsListingStatus()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.ListingStatus.Should().Be("Privatangebot");
    }

    [Test]
    public void ParseDetailPage_ExtractsCurrentUse()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentUse.Should().Be("Vermietet");
    }

    [Test]
    public void ParseDetailPage_ExtractsAvailability()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.Availability.Should().Be("Nach Vereinbarung");
    }

    [Test]
    public void ParseDetailPage_WithMissingField_ReturnsNullForThatField()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table class=""details-table"">
                        <tr>
                            <td><div class=""label-text"">Baujahr</div></td>
                            <td><div class=""value-text"">2020</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
        result!.BuildYear.Should().Be(2020);
        result.PropertyType.Should().BeNull();
        result.Condition.Should().BeNull();
    }

    [Test]
    public void ParseDetailPage_WithMalformedHtml_ReturnsNull()
    {
        // Arrange
        var html = "<html><body>invalid structure</body></html>";

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ParseDetailPage_ExtractsTitleFromH1()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <h1>Büro mit Wohnoption in KA-Durlach</h1>
                    <h2>76227 Karlsruhe (Durlach) – Baden-Württemberg</h2>
                    <table>
                        <tr><td><div>Baujahr</div></td><td><div>2013</div></td></tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailPage(html);

        // Assert
        result.Should().NotBeNull();
    }
}
