namespace FlatMonitor.Tests.ImmobilienScout24;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping;

[TestFixture]
public class ImmobilienScout24ScraperTests
{
    private ImmobilienScout24DetailParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ImmobilienScout24DetailParser();
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesAddressField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"address\": \"Daxlanden, 76189 Karlsruhe\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.Address
        metadata.Should().NotBeNull();
        metadata!.Address.Should().Be("Daxlanden, 76189 Karlsruhe");
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesPostalCodeField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"zip\": \"76189\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.PostalCode
        metadata.Should().NotBeNull();
        metadata!.PostalCode.Should().Be("76189");
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesDistrictField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"location\": \"Daxlanden\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.District
        metadata.Should().NotBeNull();
        metadata!.District.Should().Be("Daxlanden");
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesRoomsCountField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"noRooms\": 4}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.RoomsCount
        metadata.Should().NotBeNull();
        metadata!.RoomsCount.Should().Be(4);
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesLivingAreaSqmField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"livingSpace\": \"104\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.LivingAreaSqm
        metadata.Should().NotBeNull();
        metadata!.LivingAreaSqm.Should().Be(104);
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesPriceEurField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"price\": \"347.000 €\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.PriceEur
        metadata.Should().NotBeNull();
        metadata!.PriceEur.Should().Be(347000);
    }

    [Test]
    public void Parser_ExtractExposeMetadata_PopulatesCommissionPercentField()
    {
        // Arrange
        var html = "<script>var config = {commissionSplitConfig: {courtage: \"3,57% inkl. MwSt.\", expose: {}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify the field that would be used in Flat.CommissionPercent
        metadata.Should().NotBeNull();
        metadata!.CommissionPercent.Should().Be(3.57);
    }

    [Test]
    public void Parser_ExtractExposeMetadata_HandlesAllFieldsTogether()
    {
        // Arrange - realistic full expose object from IS24
        var html = "<script>var config = {commissionSplitConfig: {courtage: \"3,57% inkl. MwSt.\", expose: {\"address\": \"Daxlanden, 76189 Karlsruhe\", \"zip\": \"76189\", \"location\": \"Daxlanden\", \"livingSpace\": \"104\", \"noRooms\": 4, \"price\": \"347.000 €\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - verify all fields that populate a Flat object
        metadata.Should().NotBeNull();
        metadata!.Address.Should().Be("Daxlanden, 76189 Karlsruhe");
        metadata.PostalCode.Should().Be("76189");
        metadata.District.Should().Be("Daxlanden");
        metadata.LivingAreaSqm.Should().Be(104);
        metadata.RoomsCount.Should().Be(4);
        metadata.PriceEur.Should().Be(347000);
        metadata.CommissionPercent.Should().Be(3.57);
    }

    [Test]
    public void Parser_ExtractExposeMetadata_HandlesPartialData()
    {
        // Arrange - only some fields present
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"address\": \"Test Str. 1, 12345 City\", \"livingSpace\": \"80\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - present fields extracted, missing fields null
        metadata.Should().NotBeNull();
        metadata!.Address.Should().Be("Test Str. 1, 12345 City");
        metadata.LivingAreaSqm.Should().Be(80);
        metadata.PostalCode.Should().BeNull();
        metadata.District.Should().BeNull();
        metadata.RoomsCount.Should().BeNull();
        metadata.PriceEur.Should().BeNull();
        metadata.CommissionPercent.Should().BeNull(); // No courtage in config
    }

    [Test]
    public void Parser_ExtractExposeMetadata_ReturnsFallbackFields()
    {
        // Arrange - verify the parser provides all fields for Flat fallback
        var html = "<script>var config = {commissionSplitConfig: {expose: {\"address\": \"Main St. 5\"}}};</script>";

        // Act
        var metadata = _parser.Parse(html).Metadata;

        // Assert - ensure metadata object has all properties that Flat needs
        metadata.Should().NotBeNull();
        var metadataType = metadata!.GetType();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.Address)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.PostalCode)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.District)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.LivingAreaSqm)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.RoomsCount)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.PriceEur)).Should().NotBeNull();
        metadataType.GetProperty(nameof(ImmobilienScout24DetailParser.ExposeMetadata.CommissionPercent)).Should().NotBeNull();
    }

    [Test]
    public void Parser_ExtractExposeMetadata_ParsesVariousCommissionFormats()
    {
        // Arrange - test different commission formats
        var testCases = new[]
        {
            ("3,57% inkl. MwSt.", 3.57),
            ("2,38% zzgl. MwSt.", 2.38),
            ("4,76%", 4.76),
            ("0,5%", 0.5),
        };

        foreach (var (courtageValue, expectedPercent) in testCases)
        {
            var html = "<script>var config = {commissionSplitConfig: {courtage: \"" + courtageValue + "\", expose: {}}};</script>";

            // Act
            var metadata = _parser.Parse(html).Metadata;

            // Assert
            metadata.Should().NotBeNull();
            metadata!.CommissionPercent.Should().Be(expectedPercent, $"should parse {courtageValue} as {expectedPercent}");
        }
    }

    [Test]
    public void Parser_ImplmentsIDetailParser()
    {
        // Assert - verify parser implements the interface
        _parser.Should().BeAssignableTo<IDetailParser>();
    }
}
