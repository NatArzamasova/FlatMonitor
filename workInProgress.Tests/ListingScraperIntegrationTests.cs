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
}
