namespace FlatMonitor.Scraping.Common;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for scrapers that fetch all pages first, then enrich each flat with its details.
/// Flow: clear the HTML page archive → start a session → fetch all pages (on the flats index) → enrich and yield each
/// flat (navigate to detail pages) → dispose the session.
/// Collecting all cards first avoids losing pagination context when navigating to detail pages.
/// Derived classes only start a session; everything a run needs (browser, fetchers) lives in that session.
/// </summary>
public abstract class FlatScraper : IScraper
{
    protected FlatScraper(string source, HtmlPageArchive htmlPages, ILogger logger)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        HtmlPages = htmlPages ?? throw new ArgumentNullException(nameof(htmlPages));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Source { get; }

    /// <summary>The detail pages of the current run, for debugging; emptied when a run starts.</summary>
    protected HtmlPageArchive HtmlPages { get; }

    /// <summary>The derived scraper's logger; also handed to the helpers it creates.</summary>
    protected ILogger Logger { get; }

    public async IAsyncEnumerable<Flat> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // No catch: failures and cancellation must reach ScraperCoordinator, which records them as source errors.
        await HtmlPages.ClearAsync(ct);

        // A session that fails to start cleans up after itself; once started, it is disposed however the run ends
        await using var session = await StartAsync(ct);

        // First, fetch and parse all pages to collect all flats
        // This must happen while on the flats index (before detail navigation)
        var cards = new List<Flat>();
        int pageCount = 0;
        await foreach (var pageListings in session.FetchCardsAsync(ct))
        {
            pageCount++;
            Logger.LogInformation("{Source}: collected {Count} flats from page {Page}", Source, pageListings.Count, pageCount);
            cards.AddRange(pageListings);
        }

        Logger.LogInformation("{Source}: all pages fetched: {Pages} pages, {Count} flats collected", Source, pageCount, cards.Count);

        // Then, enrich and yield one by one
        Logger.LogInformation("{Source}: enriching {Count} flats with details", Source, cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return await EnrichOrKeepAsync(session, cards[i], i + 1, cards.Count, ct);
        }

        Logger.LogInformation("{Source}: scraping complete, {Count} flats processed", Source, cards.Count);
    }

    /// <summary>Enriches the flat; if its detail page fails, keeps the card data and continues.</summary>
    private async Task<Flat> EnrichOrKeepAsync(IScrapeSession session, Flat flat, int position, int total, CancellationToken ct)
    {
        try
        {
            var enrichedFlat = await session.EnrichAsync(flat, ct);
            Logger.LogDebug(
                "{Source}: ({Position}/{Total}) {ListingId} - Address={Address}, Price={PriceEur}, OriginalPrice={OriginalPriceEur}, " +
                "Area={LivingAreaSqm}, Rooms={RoomsCount}, Commission={CommissionPercent}",
                Source, position, total, enrichedFlat.ListingId, enrichedFlat.Address, enrichedFlat.PriceEur, enrichedFlat.OriginalPriceEur,
                enrichedFlat.LivingAreaSqm, enrichedFlat.RoomsCount, enrichedFlat.CommissionPercent);
            return enrichedFlat;
        }
        // Not on cancellation; an HttpClient timeout (TaskCanceledException) is just a failed detail page
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Logger.LogWarning(ex, "{Source}: ({Position}/{Total}) {ListingId} - error enriching details, keeping card data",
                Source, position, total, flat.ListingId);
            return flat;
        }
    }

    /// <summary>
    /// Starts a run: launches the browser, creates the fetchers, and so on. Throws if that fails, leaving nothing open.
    /// </summary>
    protected abstract Task<IScrapeSession> StartAsync(CancellationToken ct);

    /// <summary>One run of a scraper. Disposing it releases what the run opened (e.g. closes the browser).</summary>
    protected interface IScrapeSession : IAsyncDisposable
    {
        /// <summary>
        /// Fetches all pages and yields each page's parsed listing cards: basic info (ID, URL, title, ...), not yet
        /// the details.
        /// </summary>
        IAsyncEnumerable<List<Flat>> FetchCardsAsync(CancellationToken ct);

        /// <summary>The card with its detail page's data. Throws if the detail page cannot be loaded.</summary>
        Task<Flat> EnrichAsync(Flat card, CancellationToken ct);
    }
}
