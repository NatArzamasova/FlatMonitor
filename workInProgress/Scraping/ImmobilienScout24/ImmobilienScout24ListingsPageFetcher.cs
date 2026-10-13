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

    public ImmobilienScout24ListingsPageFetcher(IBrowser browser, IPage page)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>
    /// Yields raw HTML for each listings page, starting at the first page, stopping when
    /// the next-page button is disabled or after reaching the safety limit.
    /// </summary>
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int pageNumber = 1;

        while (pageNumber <= MaxPagesLimit)
        {
            Console.WriteLine($"Fetching page {pageNumber}...");

            var pageResult = await FetchPageInternalAsync(pageNumber, ct);

            if (pageResult == null)
            {
                yield break;
            }

            yield return pageResult;
            pageNumber++;
        }

        // Hard cap reached
        Console.WriteLine($"Warning: Pagination reached safety cap of {MaxPagesLimit} pages. Stopping.");
    }

    private async Task<string?> FetchPageInternalAsync(int pageNumber, CancellationToken ct)
    {
        try
        {
            // Wait for listings to load (NetworkIdle ensures page is stable)
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

            // Extract current page HTML
            var html = await _page.ContentAsync();

            // Count listing cards on this page
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var listingCards = doc.DocumentNode.SelectNodes("//div[@class='touchpoint-card']");
            int listingCount = listingCards?.Count ?? 0;

            if (listingCount == 0)
            {
                Console.WriteLine($"Page {pageNumber} returned zero listings. Pagination complete.");
                return null;
            }

            Console.WriteLine($"Page {pageNumber}: {listingCount} listings found.");

            // Check if next page button exists and is enabled
            var nextButton = await _page.QuerySelectorAsync("button[data-testid='pagination-button-next']:not([disabled])");
            if (nextButton == null)
            {
                Console.WriteLine($"Next page button not found or disabled. Pagination complete after this page.");
            }
            else
            {
                // Click next page button
                await nextButton.ClickAsync();
            }

            return html;
        }
        catch (Exception ex)
        {
            if (pageNumber == 1)
            {
                // Page 1 is fatal
                throw new InvalidOperationException(
                    $"Failed to fetch the first page from immobilienscout24.de. Scrape cannot continue.", ex);
            }

            // Later pages: log warning and stop
            Console.WriteLine($"Warning: Failed to fetch page {pageNumber}: {ex.Message}");
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
