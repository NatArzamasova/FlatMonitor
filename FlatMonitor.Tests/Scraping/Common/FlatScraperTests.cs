using NUnit.Framework;
using FluentAssertions;

namespace FlatMonitor.Tests.Scraping.Common;

using static FakeFlatScraper;

[TestFixture]
public class FlatScraperTests
{
    [Test]
    public async Task ScrapeAsync_WhenInitializationFails_ThrowsAndCleansUp()
    {
        // Arrange
        var scraper = new FakeFlatScraper { InitializeException = new InvalidOperationException("Login failed") };

        // Act
        var act = () => scraper.ScrapeAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Login failed");
        scraper.CleanupCalled.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAsync_WhenPageFetchFails_ThrowsAndCleansUp()
    {
        // Arrange
        var scraper = new FakeFlatScraper { PageFetchException = new HttpRequestException("First page unreachable") };

        // Act
        var act = () => scraper.ScrapeAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        scraper.CleanupCalled.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAsync_WhenCanceledDuringEnrichment_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var scraper = new FakeFlatScraper
        {
            Flats = [CreateFlat("1"), CreateFlat("2")],
            OnEnrich = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            },
        };

        // Act
        var act = () => scraper.ScrapeAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        scraper.CleanupCalled.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAsync_WhenEnrichmentOfOneFlatFails_KeepsBaseFlatAndContinues()
    {
        // Arrange
        var scraper = new FakeFlatScraper
        {
            Flats = [CreateFlat("1"), CreateFlat("2")],
            OnEnrich = (flat, _) =>
            {
                if (flat.ListingId == "1")
                    throw new InvalidOperationException("Detail page broken");
            },
        };

        // Act
        var result = (await scraper.ScrapeAsync()).ToList();

        // Assert
        result.Select(f => f.ListingId).Should().Equal("1", "2");
    }
}
