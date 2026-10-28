namespace FlatMonitor.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.OhneMakler;

[TestFixture]
public class OhneMaklerScraperTests
{
    [Test]
    public void OhneMaklerScraper_CanBeInstantiated()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        var scraper = new OhneMaklerScraper(httpClient);

        // Assert
        scraper.Should().NotBeNull();
    }

    [Test]
    [Explicit("Calls the live ohne-makler.net site; run with: dotnet test --filter \"TestCategory=Live\"")]
    [Category("Live")]
    public async Task OhneMaklerScraper_ScrapeAllListingsAsync_ReturnsListOfListings()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var scraper = new OhneMaklerScraper(httpClient);

        // Act & Assert
        // This test verifies the method exists and can be called.
        // Full integration testing with real HTTP requires manual verification via:
        // dotnet run -- scrape
        var result = await scraper.ScrapeAllListingsAsync();

        // The method should return a list (even if empty due to no live connection)
        result.Should().NotBeNull();
        result.Should().BeOfType<List<Flat>>();
    }

    [Test]
    public void SanitizeAddress_RemovesHtmlEntityStateSuffix()
    {
        // Arrange
        var addressWithSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) &ndash; Baden-Württemberg";

        // Act
        var result = OhneMaklerAddressHelper.SanitizeAddress(addressWithSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_RemovesUtf8StateSuffix()
    {
        // Arrange
        var addressWithSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) – Baden-Württemberg";

        // Act
        var result = OhneMaklerAddressHelper.SanitizeAddress(addressWithSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_WithoutSuffix_ReturnsUnchanged()
    {
        // Arrange
        var addressWithoutSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)";

        // Act
        var result = OhneMaklerAddressHelper.SanitizeAddress(addressWithoutSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_WithWhitespaceAroundSuffix_TrimsCorrectly()
    {
        // Arrange
        var addressWithExtraWhitespace = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)   &ndash; Baden-Württemberg   ";

        // Act
        var result = OhneMaklerAddressHelper.SanitizeAddress(addressWithExtraWhitespace);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }
}
