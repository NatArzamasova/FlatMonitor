namespace workInProgress.Scraping.Common;

using workInProgress.Models;

/// <summary>
/// Base class for scrapers that fetch all pages first, then enrich listings with details.
/// Provides the common flow: fetch all pages (on listings index) → collect all listings → enrich all with details (navigate to detail pages) → save to DB each page.
/// This avoids losing pagination context when navigating to detail pages, and saves to DB immediately to avoid memory bloat on large datasets.
/// Derived classes implement how to fetch pages, enrich listings, and save to database.
/// </summary>
public abstract class DetailedListingScraper : IScraper
{
    public abstract string Source { get; }

    public async Task<IEnumerable<Flat>> ScrapeAsync() => await ScrapeAllListingsAsync();

    public async Task<List<Flat>> ScrapeAllListingsAsync(CancellationToken ct = default)
    {
        var allListings = new List<Flat>();
        var allPageListings = new List<Flat>();
        int totalProcessed = 0;
        int pageNumber = 1;

        try
        {
            // Initialize scraper (handles browser launch, authentication, etc.)
            await InitializeAsync(ct);

            // Clear previous HTML snapshots from last run
            await ClearPreviousSnapshotsAsync(ct);

            // First, fetch and parse all pages to collect all listings
            // This must happen while on the listings index (before detail navigation)
            await foreach (var pageListings in FetchAndParseAllPagesAsync(ct))
            {
                Console.WriteLine($"--- Collected {pageListings.Count} listings from page {pageNumber} ---");
                allPageListings.AddRange(pageListings);
                pageNumber++;
            }

            Console.WriteLine($"\n=== {Source}: All pages fetched: {pageNumber - 1} total pages, {allPageListings.Count} listings collected ===\n");

            // Then, enrich listings page by page and save to DB immediately
            Console.WriteLine($"=== {Source}: Enriching and saving {allPageListings.Count} listings with details ===");
            pageNumber = 1;
            var pageBuffer = new List<Flat>();
            int totalListings = allPageListings.Count;

            foreach (var listing in allPageListings)
            {
                totalProcessed++;
                try
                {
                    var enrichedListing = await EnrichListingWithDetailsAsync(listing, ct);
                    DetailScraperLogger.LogEnrichedListing(Source, enrichedListing, totalProcessed, totalListings);
                    pageBuffer.Add(enrichedListing);
                    allListings.Add(enrichedListing);
                }
                catch (Exception ex)
                {
                    // Log error but continue with partial data
                    DetailScraperLogger.LogEnrichmentFailed(Source, listing.ListingId, ex.Message, totalProcessed, totalListings);
                    pageBuffer.Add(listing);
                    allListings.Add(listing);
                }

                // Estimate when a "page" of listings is done (e.g., every 20 items) and save to DB
                // This prevents memory bloat on large datasets like 600+ items
                if (totalProcessed % 20 == 0 || totalProcessed == allPageListings.Count)
                {
                    Console.WriteLine($"--- {Source}: Saving {pageBuffer.Count} listings to database (batch {pageNumber}) ---");
                    await SaveListingsAsync(pageBuffer, ct);
                    pageBuffer.Clear();
                    pageNumber++;
                }
            }

            Console.WriteLine($"\n=== {Source}: Scraping complete: {pageNumber - 1} batches saved, {totalProcessed}/{totalListings} listings processed ===\n");
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

        return allListings;
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
    /// Save a batch of listings to the database.
    /// Called periodically (e.g., every 20 listings) to avoid memory bloat on large datasets.
    /// </summary>
    protected abstract Task SaveListingsAsync(List<Flat> listings, CancellationToken ct);

    /// <summary>
    /// Cleanup resources (close browser, dispose HTTP client, etc.).
    /// Called once at the end of scraping.
    /// </summary>
    protected abstract Task CleanupAsync();
}
