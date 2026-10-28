using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;

namespace FlatMonitor.Tests.Scraping.Common;

/// <summary>
/// In-memory <see cref="FlatScraper"/>: returns <see cref="Flats"/> as a single page and can be told to fail at each stage.
/// </summary>
internal sealed class FakeFlatScraper(string source = "Fake") : FlatScraper
{
    public override string Source => source;

    public List<Flat> Flats { get; init; } = [];
    public Exception? InitializeException { get; init; }
    public Exception? PageFetchException { get; init; }
    public Action<Flat, CancellationToken>? OnEnrich { get; init; }
    public bool CleanupCalled { get; private set; }

    public static Flat CreateFlat(string id, string district = "Oberreut") => new()
    {
        ListingId = id,
        District = district,
        ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
    };

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
