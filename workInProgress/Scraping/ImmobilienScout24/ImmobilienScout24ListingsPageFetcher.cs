namespace workInProgress.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Playwright;

/// <summary>
/// Fetches listings pages from immobilienscout24.de using Playwright browser automation.
/// Handles pagination by detecting and clicking the next-page button until no more pages exist.
/// </summary>
internal sealed class ImmobilienScout24ListingsPageFetcher : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly IPage _page;
    private const int MaxPagesLimit = 50;
    private bool _isLastPage = false;

    public ImmobilienScout24ListingsPageFetcher(IBrowser browser, IPage page)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>
    /// Yields raw HTML for subsequent pages, starting from the next page after the current one.
    /// Assumes the browser is already on page 1; fetches pages 2, 3, etc.
    /// Stops when the next-page button is disabled or after reaching the safety limit.
    /// </summary>
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
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
            Console.WriteLine($"Fetching page {pageNumber}: Waiting for page load...");
            // Wait for DOM content first, then allow some network time for listings to load
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60000 });
            await Task.Delay(2000); // Give it 2 more seconds for listings to render
            Console.WriteLine($"Fetching page {pageNumber}: Page loaded, extracting HTML...");

            // Extract current page HTML
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

            // Check if next page button exists and is enabled
            Console.WriteLine($"Fetching page {pageNumber}: Checking for next page button...");
            var nextButton = await _page.QuerySelectorAsync("button[data-testid='pagination-button-next']:not([disabled])");
            if (nextButton == null)
            {
                Console.WriteLine($"Next page button not found or disabled. Pagination complete after this page.");
                _isLastPage = true;
            }
            else
            {
                // Click next page button
                Console.WriteLine($"Fetching page {pageNumber}: Clicking next page button...");
                await nextButton.ClickAsync();
            }

            return html;
        }
        catch (Exception ex)
        {
            if (pageNumber == 1)
            {
                // Page 1 is fatal
                Console.Error.WriteLine($"Error on page {pageNumber}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                throw new InvalidOperationException(
                    $"Failed to fetch the first page from immobilienscout24.de. Scrape cannot continue.", ex);
            }

            // Later pages: log warning and stop
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
