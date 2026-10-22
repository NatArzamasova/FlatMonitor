namespace FlatMonitor.Scraping.Common;

using FlatMonitor.Models;
using FlatMonitor.Scraping;

/// <summary>
/// Base class for scrapers that fetch all pages first, then enrich flat with details.
/// Provides the common flow: fetch all pages (on flats index) → collect all falts → enrich all with details (navigate to detail pages) → save to DB each page.
/// This avoids losing pagination context when navigating to detail pages, and saves to DB immediately to avoid memory bloat on large datasets.
/// Derived classes implement how to fetch pages, enrich falts, and save to database.
/// </summary>
public abstract class FlatScraper : IScraper
{
    public abstract string Source { get; }

    /// <summary>
    /// Callback to filter and save a batch of listings. Set by ScraperCoordinator.
    /// If null, batch saving is skipped (flats returned to caller for external handling).
    /// </summary>
    public Func<List<Flat>, CancellationToken, Task>? SaveListingsCallback { get; set; }

    public async Task<IEnumerable<Flat>> ScrapeAsync(CancellationToken ct = default, Func<int, Task>? onBatchSaved = null)
        => await ScrapeAllListingsAsync(ct, onBatchSaved);

    public async Task<List<Flat>> ScrapeAllListingsAsync(CancellationToken ct = default, Func<int, Task>? onBatchSaved = null)
    {
        var allFlats = new List<Flat>();
        var allPageFlats = new List<Flat>();
        int totalProcessed = 0;
        int pageNumber = 1;

        try
        {
            // Initialize scraper (handles browser launch, authentication, etc.)
            await InitializeAsync(ct);

            // Clear previous HTML snapshots from last run
            await ClearPreviousSnapshotsAsync(ct);

            // First, fetch and parse all pages to collect all flats
            // This must happen while on the flats index (before detail navigation)
            await foreach (var pageListings in FetchAndParseAllPagesAsync(ct))
            {
                Console.WriteLine($"--- Collected {pageListings.Count} flats from page {pageNumber} ---");
                allPageFlats.AddRange(pageListings);
                pageNumber++;
            }

            Console.WriteLine($"\n=== {Source}: All pages fetched: {pageNumber - 1} total pages, {allPageFlats.Count} flats collected ===\n");

            // Then, enrich page by page and save to DB immediately
            Console.WriteLine($"=== {Source}: Enriching and saving {allPageFlats.Count}flats with details ===");
            pageNumber = 1;
            var pageBuffer = new List<Flat>();
            int totalFlats = allPageFlats.Count;

            foreach (var flat in allPageFlats)
            {
                ct.ThrowIfCancellationRequested();
                totalProcessed++;
                try
                {
                    var enrichedFlat = await EnrichListingWithDetailsAsync(flat, ct);
                    FlatScraperLogger.LogEnrichedFlat(Source, enrichedFlat, totalProcessed, totalFlats);
                    pageBuffer.Add(enrichedFlat);
                    allFlats.Add(enrichedFlat);
                }
                catch (Exception ex)
                {
                    // Log error but continue with partial data
                    FlatScraperLogger.LogEnrichmentFailed(Source, flat.ListingId, ex.Message, totalProcessed, totalFlats);
                    pageBuffer.Add(flat);
                    allFlats.Add(flat);
                }

                // Save batch periodically (every 20 items) to avoid memory bloat
                // Filtering happens before saving via SaveListingsCallback
                if (totalProcessed % 20 == 0 || totalProcessed == allPageFlats.Count)
                {
                    if (pageBuffer.Count > 0 && SaveListingsCallback != null)
                    {
                        await SaveListingsCallback(pageBuffer, ct);
                    }
                    if (onBatchSaved != null)
                        await onBatchSaved(pageBuffer.Count);
                    pageBuffer.Clear();
                    pageNumber++;
                }
            }

            Console.WriteLine($"\n=== {Source}: Scraping complete: {pageNumber - 1} batches saved, {totalProcessed}/{totalFlats} listings processed ===\n");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scraping {Source}: {ex.Message}");
        }
        finally
        {
            // Cleanup (close browser, dispose resources, etc.)
            await CleanupAsync();
        }

        return allFlats;
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
