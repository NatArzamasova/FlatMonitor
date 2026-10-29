using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Scraping.Common;

using static FakeFlatScraper;

[TestFixture]
public class FlatScraperTests
{
    [Test]
    public async Task ScrapeAsync_WhenTheSessionFailsToStart_Throws()
    {
        // Arrange: a session that fails to start cleans up itself, so there is nothing to dispose
        var scraper = new FakeFlatScraper { StartException = new InvalidOperationException("Login failed") };

        // Act
        var act = async () => await scraper.ScrapeAsync().ToListAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Login failed");
        scraper.SessionStarted.Should().BeFalse();
    }

    [Test]
    public async Task ScrapeAsync_EmptiesTheHtmlSnapshotsBeforeStartingTheSession()
    {
        // Arrange: a page saved by the previous run
        var dir = Path.Combine(Path.GetTempPath(), $"FlatScraperTests_{Guid.NewGuid()}");
        var snapshots = new HtmlPageArchive(dir, NullLogger.Instance);
        await snapshots.SaveAsync("old", "<html></html>");
        var scraper = new SnapshotCheckingScraper(snapshots);

        try
        {
            // Act
            await scraper.ScrapeAsync().ToListAsync();

            // Assert
            scraper.SnapshotsWhenStarted.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Records which HTML snapshots exist when its session starts; yields nothing.</summary>
    private sealed class SnapshotCheckingScraper(HtmlPageArchive snapshots)
        : FlatScraper("Checking", snapshots, NullLogger.Instance)
    {
        public string[] SnapshotsWhenStarted { get; private set; } = [];

        protected override Task<IScrapeSession> StartAsync(CancellationToken ct)
        {
            SnapshotsWhenStarted = Directory.Exists(HtmlPages.Directory) ? Directory.GetFiles(HtmlPages.Directory) : [];
            return Task.FromResult<IScrapeSession>(new EmptySession());
        }

        private sealed class EmptySession : IScrapeSession
        {
            public IAsyncEnumerable<List<Flat>> FetchCardsAsync(CancellationToken ct) => AsyncEnumerable.Empty<List<Flat>>();

            public Task<Flat> EnrichAsync(Flat card, CancellationToken ct) => Task.FromResult(card);

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
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
        scraper.SessionDisposed.Should().BeTrue();
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
        scraper.SessionDisposed.Should().BeTrue();
    }

    [Test]
    public async Task ScrapeAsync_WhenADetailPageTimesOut_KeepsBaseFlatAndContinues()
    {
        // Arrange: an HttpClient timeout is a TaskCanceledException although nothing was canceled
        var scraper = new FakeFlatScraper
        {
            Flats = [CreateFlat("1"), CreateFlat("2")],
            OnEnrich = (flat, _) =>
            {
                if (flat.ListingId == "1")
                    throw new TaskCanceledException("The request timed out", new TimeoutException());
            },
        };

        // Act
        var result = await scraper.ScrapeAsync().ToListAsync();

        // Assert
        result.Select(f => f.ListingId).Should().Equal("1", "2");
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
    public async Task ScrapeAsync_LogsEnrichedFlatsAtDebugAndFailuresAtWarningWithTheException()
    {
        // Arrange
        var error = new InvalidOperationException("Detail page broken");
        var logger = new ListLogger<FlatScraperTests>();
        var scraper = new FakeFlatScraper("OhneMakler", logger)
        {
            Flats = [CreateFlat("1"), CreateFlat("2")],
            OnEnrich = (flat, _) =>
            {
                if (flat.ListingId == "1")
                    throw error;
            },
        };

        // Act
        await scraper.ScrapeAsync().ToListAsync();

        // Assert
        var warning = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
        warning.Message.Should().Be("OhneMakler: (1/2) 1 - error enriching details, keeping card data");
        warning.Exception.Should().BeSameAs(error);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Debug)
            .Which.Message.Should().StartWith("OhneMakler: (2/2) 2 - ");
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
        scraper.SessionDisposed.Should().BeTrue();
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
        scraper.SessionDisposed.Should().BeTrue();
    }
}
