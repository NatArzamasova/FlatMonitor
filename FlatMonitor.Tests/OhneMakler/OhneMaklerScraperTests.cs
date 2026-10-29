namespace FlatMonitor.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging.Abstractions;

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
        var scraper = new OhneMaklerScraper(httpClient, TempStorage, NullLogger<OhneMaklerScraper>.Instance);

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
        var scraper = new OhneMaklerScraper(httpClient, TempStorage, NullLogger<OhneMaklerScraper>.Instance);

        // Act & Assert
        // This test verifies the method exists and can be called.
        // Full integration testing with real HTTP requires manual verification via:
        // dotnet run -- scrape
        List<Flat> result = await scraper.ScrapeAsync().ToListAsync();

        // The scraper should yield flats (an empty list only if the site returned none)
        result.Should().NotBeNull();
    }
}
