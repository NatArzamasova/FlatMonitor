namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Playwright;

/// <summary>
/// Fetches flats pages from immobilienscout24.de using Playwright browser automation.
/// Handles pagination by detecting and clicking the next-page button until no more pages exist.
/// </summary>
internal sealed class ImmobilienScout24FlatsPageFetcher : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly IPage _page;
    private const int MaxPagesLimit = 50;
    private bool _isLastPage = false;

    public ImmobilienScout24FlatsPageFetcher(IBrowser browser, IPage page)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>
    /// Yields raw HTML for all pages, starting from page 2 onwards.
    /// Assumes the browser is already on page 1; fetches pages 2, 3, etc.
    /// Stops when the next-page button is disabled or after reaching the safety limit.
    /// </summary>
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _isLastPage = false; // Reset for each new scrape
        int pageNumber = 2;

        while (pageNumber <= MaxPagesLimit)
        {
            Console.WriteLine($"Fetching page {pageNumber}...");

            var pageResult = await FetchPageInternalAsync(pageNumber, ct);

            if (pageResult == null)
            {
                yield break;
            }

            yield return pageResult;

            // Check if this was the last page
            if (_isLastPage)
            {
                Console.WriteLine("Pagination complete.");
                yield break;
            }

            pageNumber++;
        }

        // Hard cap reached
        Console.WriteLine($"Warning: Pagination reached safety cap of {MaxPagesLimit} pages. Stopping.");
    }

    private async Task<string?> FetchPageInternalAsync(int pageNumber, CancellationToken ct)
    {
        try
        {
            // Click next page button to navigate to the target page
            Console.WriteLine($"Fetching page {pageNumber}: Clicking next page button...");
            var nextButton = await _page.QuerySelectorAsync("button[data-testid='pagination-button-next']:not([disabled])");
            if (nextButton == null)
            {
                Console.WriteLine($"Next page button not found or disabled. Pagination complete.");
                _isLastPage = true;
                return null;
            }

            await nextButton.ClickAsync();

            // Wait for the next page to load after clicking
            Console.WriteLine($"Fetching page {pageNumber}: Waiting for page load...");
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60000 });
            await Task.Delay(2000); // Give it 2 more seconds for listings to render
            Console.WriteLine($"Fetching page {pageNumber}: Page loaded, extracting HTML...");

            // Extract current page HTML (now on the new page)
            var html = await _page.ContentAsync();
            Console.WriteLine($"Fetching page {pageNumber}: HTML extracted ({html.Length} bytes), parsing...");

            // Count listing cards on this page
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var listingCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'listing-card') and @data-obid]");
            int listingCount = listingCards?.Count ?? 0;
            Console.WriteLine($"Fetching page {pageNumber}: Found {listingCount} listing cards");

            if (listingCount == 0)
            {
                Console.WriteLine($"Page {pageNumber} returned zero listings. Pagination complete.");
                return null;
            }

            Console.WriteLine($"Page {pageNumber}: {listingCount} listings found.");
            return html;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to fetch page {pageNumber}: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_page != null)
            await _page.CloseAsync();

        if (_browser != null)
            await _browser.CloseAsync();
    }
}
