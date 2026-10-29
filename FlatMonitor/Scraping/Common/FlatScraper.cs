namespace FlatMonitor.Scraping.Common;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for scrapers that fetch all pages first, then enrich each flat with its details.
/// Flow: initialize → fetch all pages (on the flats index) → enrich and yield each flat (navigate to detail pages) → cleanup.
/// Collecting all cards first avoids losing pagination context when navigating to detail pages.
/// Derived classes implement how to initialize, fetch pages, enrich flats, and clean up.
/// </summary>
public abstract class FlatScraper : IScraper
{
    protected FlatScraper(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string Source { get; }

    /// <summary>The derived scraper's logger; also handed to the helpers it creates.</summary>
    protected ILogger Logger { get; }

    public async IAsyncEnumerable<Flat> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // No catch: failures and cancellation must reach ScraperCoordinator, which records them as source errors.
        try
        {
            // Initialize scraper (handles browser launch, authentication, etc.)
            await InitializeAsync(ct);

            // Clear previous HTML snapshots from last run
            await ClearPreviousSnapshotsAsync(ct);

            // First, fetch and parse all pages to collect all flats
            // This must happen while on the flats index (before detail navigation)
            var cards = new List<Flat>();
            int pageCount = 0;
            await foreach (var pageListings in FetchAndParseAllPagesAsync(ct))
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
                yield return await EnrichOrKeepAsync(cards[i], i + 1, cards.Count, ct);
            }

            Logger.LogInformation("{Source}: scraping complete, {Count} flats processed", Source, cards.Count);
        }
        finally
        {
            // Cleanup (close browser, dispose resources, etc.)
            await CleanupAsync();
        }
    }

    /// <summary>Enriches the flat; if its detail page fails, keeps the card data and continues.</summary>
    private async Task<Flat> EnrichOrKeepAsync(Flat flat, int position, int total, CancellationToken ct)
    {
        try
        {
            var enrichedFlat = await EnrichListingWithDetailsAsync(flat, ct);
            Logger.LogDebug(
                "{Source}: ({Position}/{Total}) {ListingId} - Address={Address}, Price={PriceEur}, OriginalPrice={OriginalPriceEur}, " +
                "Area={LivingAreaSqm}, Rooms={RoomsCount}, Commission={CommissionPercent}",
                Source, position, total, enrichedFlat.ListingId, enrichedFlat.Address, enrichedFlat.PriceEur, enrichedFlat.OriginalPriceEur,
                enrichedFlat.LivingAreaSqm, enrichedFlat.RoomsCount, enrichedFlat.Details?.CommissionPercent);
            return enrichedFlat;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "{Source}: ({Position}/{Total}) {ListingId} - error enriching details, keeping card data",
                Source, position, total, flat.ListingId);
            return flat;
        }
    }

    /// <summary>
    /// Initialize the scraper (launch browser, authenticate, setup HTTP client, etc.).
    /// Called once at the start of scraping.
    /// </summary>
    protected abstract Task InitializeAsync(CancellationToken ct);

    /// <summary>
    /// Clear previous HTML snapshots from the last scrape run.
    /// Called after initialization but before fetching pages.
    /// </summary>
    protected virtual Task ClearPreviousSnapshotsAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fetch all pages and yield parsed listing cards.
    /// Listing cards should have basic info (ID, URL, title, etc.) but may not have full details yet.
    /// </summary>
    protected abstract IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync(CancellationToken ct);

    /// <summary>
    /// Enrich a single listing by fetching and parsing its detail page.
    /// Returns a new Flat object with all available data filled in.
    /// </summary>
    protected abstract Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct);

    /// <summary>
    /// Cleanup resources (close browser, dispose HTTP client, etc.).
    /// Called once at the end of scraping.
    /// </summary>
    protected abstract Task CleanupAsync();
}
