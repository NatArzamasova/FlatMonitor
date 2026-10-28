using System.Runtime.CompilerServices;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Scraping.Common;

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

    [Test]
    public async Task ScraperCoordinator_WhenFlatScraperInitializationFails_RecordsSourceErrorAndScrapesOtherSources()
    {
        // Arrange
        var failing = new FakeFlatScraper("ImmobilienScout24") { InitializeException = new InvalidOperationException("Login failed") };
        var working = new FakeFlatScraper("OhneMakler") { Flats = [CreateFlat("1", "Durlach")] };

        var repository = new Mock<ISnapshotRepository>();
        repository.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.UpsertListingsAsync(It.IsAny<List<Flat>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dataDir = Path.Combine(Path.GetTempPath(), $"FlatScraperTests_{Guid.NewGuid()}");
        var coordinator = new ScraperCoordinator(new IScraper[] { failing, working }, repository.Object, new SnapshotWriter(), dataDir);

        try
        {
            // Act
            var total = await coordinator.ScrapeAllSourcesAsync();

            // Assert
            coordinator.ScraperErrors.Should().ContainSingle()
                .Which.Should().Be("ImmobilienScout24: Login failed");
            total.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
    }

    private static Flat CreateFlat(string id, string district = "Oberreut") => new()
    {
        ListingId = id,
        District = district,
        ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
    };

    private sealed class FakeFlatScraper(string source = "Fake") : FlatScraper
    {
        public override string Source => source;

        public List<Flat> Flats { get; init; } = [];
        public Exception? InitializeException { get; init; }
        public Exception? PageFetchException { get; init; }
        public Action<Flat, CancellationToken>? OnEnrich { get; init; }
        public bool CleanupCalled { get; private set; }

        protected override Task InitializeAsync(CancellationToken ct) =>
            InitializeException != null ? Task.FromException(InitializeException) : Task.CompletedTask;

        protected override async IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            if (PageFetchException != null)
                throw PageFetchException;
            yield return Flats;
        }

        protected override Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct)
        {
            OnEnrich?.Invoke(baseListing, ct);
            return Task.FromResult(baseListing);
        }

        protected override Task CleanupAsync()
        {
            CleanupCalled = true;
            return Task.CompletedTask;
        }
    }
}
