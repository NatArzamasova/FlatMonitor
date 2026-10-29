namespace FlatMonitor.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Scraping.OhneMakler;


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

    // ==================== Parse: everything from one detail page ====================

    [Test]
    public void Parse_LiveLayout_ReadsEveryValue()
    {
        var page = _parser.Parse(File.ReadAllText(GetFixturePath("detail-page-live-layout.html")));

        page.Title.Should().Be("Helle Altbauwohnung mit Balkon in der Weststadt");
        page.Address.Should().Be("Musterstraße 12, 76133 Karlsruhe (Weststadt)");
        page.Rooms.Should().Be(3);
        page.Area.Should().Be(74.5);
        page.Price.Should().Be(389000);
        page.Details.Should().NotBeNull();
        page.Details!.BuildYear.Should().Be(1912);
        page.Details.PropertyType.Should().Be("Etagenwohnung");
        page.Details.Heating.Should().Be("Gas-Etagenheizung");
        page.Details.ServiceChargeEur.Should().Be(310);
    }

    [Test]
    public void Parse_SampleFixture_AddressIsNullWhenAnH2FollowsTheTitle()
    {
        // The older sample has the location as an <h2>; the address search stops at the first h2/h3
        var page = _parser.Parse(File.ReadAllText(GetFixturePath("detail-page-sample.html")));

        page.Title.Should().Be("Altbauperle Weststadt - Zwei Appartements in einer Wohnung");
        page.Address.Should().BeNull();
        page.Rooms.Should().Be(3);
        page.Area.Should().BeNull("'82' and 'Wohnfläche (m²)' are not adjacent in that markup");
        page.Price.Should().BeNull();
    }

    [Test]
    public void Parse_Area_TakesTheFirstPlausibleValueOnThePage()
    {
        // The 4 m² cellar is below the 5 m² minimum; 55 m² is the first plausible area
        var html = "<html><body><p>Keller 4 m²</p><span>55 m²</span><p>Grundstück 480 m²</p></body></html>";

        _parser.Parse(html).Area.Should().Be(55);
    }

    [Test]
    public void Parse_Rooms_KeepsHalfRooms()
    {
        _parser.Parse("<html><body><h1>Wohnung</h1><span>2,5 Zimmer</span></body></html>").Rooms.Should().Be(2.5);
    }

    [Test]
    public void Parse_Area_AcceptsDecimalCommaAndM2()
    {
        _parser.Parse("<html><body><span>17,90 m2</span></body></html>").Area.Should().Be(17.9);
    }

    [Test]
    public void Parse_WithoutTitle_LeavesTitleAddressAndPriceEmpty()
    {
        var page = _parser.Parse("<html><body><p>Nur Text</p></body></html>");

        page.Title.Should().BeNull();
        page.Address.Should().BeNull();
        page.Price.Should().BeNull();
        page.Area.Should().BeNull();
        page.Rooms.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_ReturnsTheDetailsOfParse()
    {
        var html = File.ReadAllText(GetFixturePath("detail-page-live-layout.html"));

        _parser.ParseDetailFields(html).Should().BeEquivalentTo(_parser.Parse(html).Details);
    }

    // The address follows the h1 title; ohne-makler.net appends the state after a dash
    [TestCase("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) &ndash; Baden-Württemberg", TestName = "Address_HtmlEntityStateSuffix_IsRemoved")]
    [TestCase("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) – Baden-Württemberg", TestName = "Address_Utf8StateSuffix_IsRemoved")]
    [TestCase("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)", TestName = "Address_WithoutSuffix_IsUnchanged")]
    [TestCase("  Sophienstr. 57b,\n   76133 Karlsruhe (Innenstadt-West)   &ndash; Baden-Württemberg   ", TestName = "Address_ExtraWhitespace_IsCollapsed")]
    public void Parse_Address_DropsTheStateSuffix(string addressText)
    {
        var html = $"<html><body><h1>Wohnung</h1>{addressText}<h2>Details</h2></body></html>";

        _parser.Parse(html).Address.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void Parse_Address_DecodesOtherEntities()
    {
        var html = "<html><body><h1>Wohnung</h1><a>Fuchs &amp; Hase Weg 1, 76133 Karlsruhe</a><h2>Details</h2></body></html>";

        _parser.Parse(html).Address.Should().Be("Fuchs & Hase Weg 1, 76133 Karlsruhe");
    }

    [TestCase("<table><tr><td><div>Aktuelle Nutzung</div></td><td><div>Eigennutzung</div></td></tr></table>", "Eigennutzung", TestName = "CurrentUse_AktuelleNutzungRow")]
    [TestCase("<table><tr><td><div>Nutzung</div></td><td><div>Vermietet</div></td></tr></table>", "Vermietet", TestName = "CurrentUse_NutzungFallback")]
    [TestCase("<table><tr><td><div>Heizung</div></td><td><div>Gas</div></td></tr></table>", null, TestName = "CurrentUse_Missing_IsNull")]
    public void ParseDetailFields_ReadsCurrentUseFromItsLabel(string body, string? expected)
    {
        var details = _parser.ParseDetailFields($"<html><body>{body}</body></html>");

        details!.CurrentUse.Should().Be(expected);
    }
}
