namespace workInProgress.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping.OhneMakler;

[TestFixture]
public class OhneMaklerDetailPageParserTests
{
    private OhneMaklerDetailPageParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new OhneMaklerDetailPageParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "OhneMakler", filename);
    }

    [Test]
    public void ParseDetailFields_WithValidHtml_ReturnsDetails()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public void ParseDetailFields_ExtractsPropertyTypeCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyType.Should().Be("Dachgeschosswohnung");
    }

    [Test]
    public void ParseDetailFields_ExtractsBuildYearAsInteger()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.BuildYear.Should().Be(1904);
    }

    [Test]
    public void ParseDetailFields_ExtractsConditionCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.Condition.Should().Be("gepflegt");
    }

    [Test]
    public void ParseDetailFields_ExtractsHeatingCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.Heating.Should().Be("Ofenheizung");
    }

    [Test]
    public void ParseDetailFields_ExtractsServiceChargeAsInteger()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceChargeEur.Should().Be(405);
    }

    [Test]
    public void ParseDetailFields_ExtractsServiceChargeFromHausgeldMtl()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table>
                        <tr>
                            <td><div>Hausgeld mtl.</div></td>
                            <td><div>300 €</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceChargeEur.Should().Be(300);
    }

    [Test]
    public void ParseDetailFields_PrefersHausgeldMtlOverNebenkosten()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table>
                        <tr>
                            <td><div>Hausgeld mtl.</div></td>
                            <td><div>300 €</div></td>
                        </tr>
                        <tr>
                            <td><div>Nebenkosten</div></td>
                            <td><div>200 €</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceChargeEur.Should().Be(300, "should prefer 'Hausgeld mtl.' over 'Nebenkosten'");
    }

    [Test]
    public void ParseDetailFields_FallsBackToNebenkosten()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table>
                        <tr>
                            <td><div>Nebenkosten</div></td>
                            <td><div>250 €</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceChargeEur.Should().Be(250, "should extract from 'Nebenkosten' when 'Hausgeld mtl.' is not present");
    }

    [Test]
    public void ParseDetailFields_ExtractsEnergyCertificateType()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyCertificateType.Should().Contain("nicht erforderlich");
    }

    [Test]
    public void ParseDetailFields_ExtractsPrimaryEnergySource()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.PrimaryEnergySource.Should().Be("Gas");
    }

    [Test]
    public void ParseDetailFields_ExtractsListingStatus()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.ListingStatus.Should().Be("Privatangebot");
    }

    [Test]
    public void ParseDetailFields_ExtractsCurrentUse()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentUse.Should().Be("Vermietet");
    }

    [Test]
    public void ParseDetailFields_ExtractsAvailability()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.Availability.Should().Be("Nach Vereinbarung");
    }

    [Test]
    public void ParseDetailFields_WithMissingField_ReturnsNullForThatField()
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
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.BuildYear.Should().Be(2020);
        result.PropertyType.Should().BeNull();
        result.Condition.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_WithMalformedHtml_ReturnsNull()
    {
        // Arrange
        var html = "<html><body>invalid structure</body></html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_ExtractsTitleFromH1()
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
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public void ParseDetailFields_ExtractsEnergyEfficiencyClass()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table>
                        <tr>
                            <td><div>Energieeffizienzklasse</div></td>
                            <td><div>C</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyEfficiencyClass.Should().Be("C");
    }

    [Test]
    public void ParseDetailFields_ExtractsEnergyConsumption()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table>
                        <tr>
                            <td><div>Endenergieverbrauch</div></td>
                            <td><div>120 kWh/(m²·a)</div></td>
                        </tr>
                    </table>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyConsumption.Should().Be("120 kWh/(m²·a)");
    }
}
