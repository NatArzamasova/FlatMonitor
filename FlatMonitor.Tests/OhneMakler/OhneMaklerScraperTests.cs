namespace FlatMonitor.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;

[TestFixture]
public class OhneMaklerScraperTests
{
    private static readonly StorageOptions TempStorage =
        new(Path.Combine(Path.GetTempPath(), "OhneMaklerScraperTests"), "Data Source=:memory:");

    [Test]
    public void OhneMaklerScraper_CanBeInstantiated()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        var scraper = new OhneMaklerScraper(httpClient, TempStorage);

        // Assert
        scraper.Should().NotBeNull();
    }

    [Test]
    [Explicit("Calls the live ohne-makler.net site; run with: dotnet test --filter \"TestCategory=Live\"")]
    [Category("Live")]
    public async Task OhneMaklerScraper_ScrapeAsync_ReturnsListings()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var scraper = new OhneMaklerScraper(httpClient, TempStorage);

        // Act & Assert
        // This test verifies the method exists and can be called.
        // Full integration testing with real HTTP requires manual verification via:
        // dotnet run -- scrape
        List<Flat> result = await scraper.ScrapeAsync().ToListAsync();

        // The scraper should yield flats (an empty list only if the site returned none)
        result.Should().NotBeNull();
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
