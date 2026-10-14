namespace workInProgress.Tests.ImmobilienScout24;

using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping;
using workInProgress.Scraping.ImmobilienScout24;

[TestFixture]
public class ImmobilienScout24DetailParserTests
{
    private ImmobilienScout24DetailParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ImmobilienScout24DetailParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "ImmobilienScout24", filename);
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
        result!.PropertyType.Should().Be("Etagenwohnung");
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
        result!.BuildYear.Should().Be(1976);
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
        result!.Heating.Should().Be("Fernwärme");
    }

    [Test]
    public void ParseDetailFields_ExtractsPrimaryEnergySourceCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.PrimaryEnergySource.Should().Be("Fernwärme");
    }

    [Test]
    public void ParseDetailFields_ExtractsEnergyEfficiencyClassFromImgAltAttribute()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("detail-page-sample.html"));

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyEfficiencyClass.Should().Be("D", "should extract from img alt attribute");
    }

    [Test]
    public void ParseDetailFields_ExtractsEnergyEfficiencyClassFromImgAltAttribute_WithDifferentClasses()
    {
        // Arrange - test with different energy classes
        var testCases = new[] { "A", "B", "C", "D", "E", "F", "G" };

        foreach (var energyClass in testCases)
        {
            var html = $@"
                <html>
                    <body>
                        <dl class='grid' role='presentation'>
                            <dt class='is24qa-energieeffizienzklasse-label'>Energieeffizienzklasse</dt>
                            <dd class='is24qa-energieeffizienzklasse grid-item three-fifths'>
                                <img alt='{energyClass}' src='/expose/img/energyEfficiencyClass/{energyClass}.svg'>
                            </dd>
                        </dl>
                    </body>
                </html>";

            // Act
            var result = _parser.ParseDetailFields(html);

            // Assert
            result.Should().NotBeNull();
            result!.EnergyEfficiencyClass.Should().Be(energyClass);
        }
    }

    [Test]
    public void ParseDetailFields_WithMissingEnergyEfficiencyClass_ReturnsNull()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <dl class='grid' role='presentation'>
                        <dt class='is24qa-baujahr-label'>Baujahr</dt>
                        <dd class='is24qa-baujahr grid-item three-fifths'>2020</dd>
                    </dl>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.EnergyEfficiencyClass.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_WithMissingFields_ReturnsNullForMissingFields()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <dl class='grid' role='presentation'>
                        <dt class='is24qa-typ-label'>Typ</dt>
                        <dd class='is24qa-typ grid-item three-fifths'>Etagenwohnung</dd>
                    </dl>
                </body>
            </html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyType.Should().Be("Etagenwohnung");
        result.BuildYear.Should().BeNull();
        result.Heating.Should().BeNull();
        result.EnergyEfficiencyClass.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_WithMalformedHtml_ReturnsEmptyDetails()
    {
        // Arrange
        var html = "<html><body>invalid structure without required classes</body></html>";

        // Act
        var result = _parser.ParseDetailFields(html);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyType.Should().BeNull();
        result.BuildYear.Should().BeNull();
        result.Heating.Should().BeNull();
    }

    [Test]
    public void ExtractAddress_WithValidHtml_ReturnsAddress()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <div data-qa='is24-expose-address'>
                        <span class='address-text'>123 Test Street, 12345 Berlin</span>
                    </div>
                </body>
            </html>";

        // Act
        var result = _parser.ExtractAddress(html);

        // Assert
        result.Should().Be("123 Test Street, 12345 Berlin");
    }

    [Test]
    public void ExtractAddress_WithoutAddress_ReturnsNull()
    {
        // Arrange
        var html = "<html><body>no address data</body></html>";

        // Act
        var result = _parser.ExtractAddress(html);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ParseDetailFields_ImplmentsIDetailParser()
    {
        // Assert
        _parser.Should().BeAssignableTo<IDetailParser>();
    }

    [Test]
    public void ExtractExposeMetadata_ParsesLivingAreaSqmFromLivingSpace()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"livingSpace\": \"104\"}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.LivingAreaSqm.Should().Be(104);
    }

    [Test]
    public void ExtractExposeMetadata_ParsesCommissionPercentFromCourtage()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {courtage: \"3,57% inkl. MwSt.\", expose: {}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.CommissionPercent.Should().Be(3.57);
    }

    [Test]
    public void ExtractExposeMetadata_AllFieldsExtractedCorrectly()
    {
        // Arrange - comprehensive test with all fields
        var html = "<script>var config = {commissionSplitConfig: {courtage: \"3,57% inkl. MwSt.\", expose: {\"address\": \"Daxlanden, 76189 Karlsruhe\", \"zip\": \"76189\", \"location\": \"Daxlanden\", \"livingSpace\": \"104\", \"noRooms\": 4, \"price\": \"347.000 €\"}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be("Daxlanden, 76189 Karlsruhe");
        result.PostalCode.Should().Be("76189");
        result.District.Should().Be("Daxlanden");
        result.LivingAreaSqm.Should().Be(104);
        result.RoomsCount.Should().Be(4);
        result.PriceEur.Should().Be(347000);
        result.CommissionPercent.Should().Be(3.57);
    }

    [Test]
    public void ExtractExposeMetadata_WithValidJson_ReturnsMetadata()
    {
        // Arrange - use valid JSON (double quotes, not single quotes)
        var html = @"
            <html>
                <body>
                    <script>
                        var config = {
                            commissionSplitConfig: {
                                expose: {""address"": ""Daxlanden, 76189 Karlsruhe"", ""zip"": ""76189"", ""location"": ""Daxlanden"", ""livingSpace"": ""104"", ""noRooms"": 4, ""price"": ""347.000 €""}
                            }
                        };
                    </script>
                </body>
            </html>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be("Daxlanden, 76189 Karlsruhe");
        result.PostalCode.Should().Be("76189");
        result.District.Should().Be("Daxlanden");
        result.LivingAreaSqm.Should().Be(104);
        result.RoomsCount.Should().Be(4);
        result.PriceEur.Should().Be(347000);
    }

    [Test]
    public void ExtractExposeMetadata_ExtractsPostalCodeFromZip()
    {
        // Arrange
        var html = @"
            <script>
                var config = {commissionSplitConfig: {expose: {""zip"": ""76189""}}};
            </script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.PostalCode.Should().Be("76189");
    }

    [Test]
    public void ExtractExposeMetadata_ExtractsDistrictFromLocation()
    {
        // Arrange
        var html = @"<script>var config = {commissionSplitConfig: {expose: {""location"": ""Daxlanden""}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.District.Should().Be("Daxlanden");
    }

    [Test]
    public void ExtractExposeMetadata_ParsesLivingSpaceAsInteger()
    {
        // Arrange
        var html = @"<script>var config = {commissionSplitConfig: {expose: {""livingSpace"": ""104""}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.LivingAreaSqm.Should().Be(104);
    }

    [Test]
    public void ExtractExposeMetadata_ParsesPriceAsInteger()
    {
        // Arrange
        var html = @"<script>var config = {commissionSplitConfig: {expose: {""price"": ""347.000 €""}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.PriceEur.Should().Be(347000);
    }

    [Test]
    public void ExtractExposeMetadata_WithMissingConfig_ReturnsNull()
    {
        // Arrange
        var html = "<html><body>no config script</body></html>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ExtractExposeMetadata_WithMissingFields_ReturnsMetadataWithNullFields()
    {
        // Arrange
        var html = @"<script>var config = {commissionSplitConfig: {expose: {""address"": ""Test Address""}}};</script>";

        // Act
        var result = _parser.ExtractExposeMetadata(html);

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be("Test Address");
        result.PostalCode.Should().BeNull();
        result.District.Should().BeNull();
        result.LivingAreaSqm.Should().BeNull();
        result.RoomsCount.Should().BeNull();
        result.PriceEur.Should().BeNull();
    }
}
