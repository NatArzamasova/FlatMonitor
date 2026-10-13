namespace workInProgress.Tests;

using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping;
using workInProgress.Models;

[TestFixture]
public class ListingScraperIntegrationTests
{
    [Test]
    public void ListingScraper_CanBeInstantiated()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        var scraper = new ListingScraper(httpClient);

        // Assert
        scraper.Should().NotBeNull();
    }

    [Test]
    public async Task ListingScraper_ScrapeAllListingsAsync_ReturnsListOfListings()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var scraper = new ListingScraper(httpClient);

        // Act & Assert
        // This test verifies the method exists and can be called.
        // Full integration testing with real HTTP requires manual verification via:
        // dotnet run -- scrape
        var result = await scraper.ScrapeAllListingsAsync();

        // The method should return a list (even if empty due to no live connection)
        result.Should().NotBeNull();
        result.Should().BeOfType<List<Listing>>();
    }

    [Test]
    public void SanitizeAddress_RemovesHtmlEntityStateSuffix()
    {
        // Arrange
        var addressWithSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) &ndash; Baden-Württemberg";

        // Act
        var result = AddressHelper.SanitizeAddress(addressWithSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_RemovesUtf8StateSuffix()
    {
        // Arrange
        var addressWithSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West) – Baden-Württemberg";

        // Act
        var result = AddressHelper.SanitizeAddress(addressWithSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_WithoutSuffix_ReturnsUnchanged()
    {
        // Arrange
        var addressWithoutSuffix = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)";

        // Act
        var result = AddressHelper.SanitizeAddress(addressWithoutSuffix);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }

    [Test]
    public void SanitizeAddress_WithWhitespaceAroundSuffix_TrimsCorrectly()
    {
        // Arrange
        var addressWithExtraWhitespace = "Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)   &ndash; Baden-Württemberg   ";

        // Act
        var result = AddressHelper.SanitizeAddress(addressWithExtraWhitespace);

        // Assert
        result.Should().Be("Sophienstr. 57b, 76133 Karlsruhe (Innenstadt-West)");
    }
}
