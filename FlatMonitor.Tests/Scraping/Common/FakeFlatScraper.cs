using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlatMonitor.Tests.Scraping.Common;

/// <summary>
/// In-memory <see cref="FlatScraper"/>: returns <see cref="Flats"/> as a single page and can be told to fail at each stage.
/// Its HTML snapshot directory is never created (nothing is saved), so clearing it does nothing.
/// </summary>
internal sealed class FakeFlatScraper(string source = "Fake", ILogger? logger = null)
    : FlatScraper(
        source,
        new HtmlSnapshotStore(Path.Combine(Path.GetTempPath(), $"FakeFlatScraper_{Guid.NewGuid()}"), NullLogger.Instance),
        logger ?? NullLogger.Instance)
{
    public List<Flat> Flats { get; init; } = [];
    public Exception? StartException { get; init; }
    public Exception? PageFetchException { get; init; }
    public Action<Flat, CancellationToken>? OnEnrich { get; init; }
    public bool SessionStarted { get; private set; }
    public bool SessionDisposed { get; private set; }

    public static Flat CreateFlat(string id, string district = "Oberreut") => new()
    {
        Source = "OhneMakler",
        ListingId = id,
        District = district,
        ScrapedAtUtc = DateTime.UtcNow,
    };

    protected override Task<IScrapeSession> StartAsync(CancellationToken ct)
    {
        if (StartException != null)
            return Task.FromException<IScrapeSession>(StartException);

        SessionStarted = true;
        return Task.FromResult<IScrapeSession>(new Session(this));
    }

    private sealed class Session(FakeFlatScraper scraper) : IScrapeSession
    {
        public async IAsyncEnumerable<List<Flat>> FetchCardsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            if (scraper.PageFetchException != null)
                throw scraper.PageFetchException;
            yield return scraper.Flats;
        }

        public Task<Flat> EnrichAsync(Flat card, CancellationToken ct)
        {
            scraper.OnEnrich?.Invoke(card, ct);
            return Task.FromResult(card);
        }

        public ValueTask DisposeAsync()
        {
            scraper.SessionDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
