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
        var act = async () => await scraper.ScrapeAsync().ToListAsync();

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
        var act = async () => await scraper.ScrapeAsync().ToListAsync();

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
        var act = async () => await scraper.ScrapeAsync(cts.Token).ToListAsync();

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
        var result = await scraper.ScrapeAsync().ToListAsync();

        // Assert
        result.Select(f => f.ListingId).Should().Equal("1", "2");
    }

    [Test]
    public async Task ScrapeAsync_YieldsEachFlatAsSoonAsItIsEnriched()
    {
        // Arrange
        var enriched = new List<string>();
        var scraper = new FakeFlatScraper
        {
            Flats = [CreateFlat("1"), CreateFlat("2"), CreateFlat("3")],
            OnEnrich = (flat, _) => enriched.Add(flat.ListingId),
        };

        // Act: record which flats were enriched when each one arrives
        var enrichedWhenYielded = new List<int>();
        await foreach (var _ in scraper.ScrapeAsync())
            enrichedWhenYielded.Add(enriched.Count);

        // Assert
        enrichedWhenYielded.Should().Equal(1, 2, 3);
        scraper.CleanupCalled.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAsync_WhenConsumerStopsEarly_CleansUpWithoutEnrichingTheRest()
    {
        // Arrange
        var enriched = new List<string>();
        var scraper = new FakeFlatScraper
        {
            Flats = [CreateFlat("1"), CreateFlat("2"), CreateFlat("3")],
            OnEnrich = (flat, _) => enriched.Add(flat.ListingId),
        };

        // Act
        await foreach (var _ in scraper.ScrapeAsync())
            break;

        // Assert
        enriched.Should().Equal("1");
        scraper.CleanupCalled.Should().BeTrue();
    }
}
