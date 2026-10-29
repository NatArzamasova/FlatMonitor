namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches flats pages from immobilienscout24.de using Playwright browser automation.
/// Handles pagination by detecting and clicking the next-page button until no more pages exist.
/// </summary>
internal sealed class ImmobilienScout24FlatsPageFetcher : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly IPage _page;
    private readonly ILogger _logger;
    private const int MaxPagesLimit = 50;
    private bool _isLastPage = false;

    public ImmobilienScout24FlatsPageFetcher(IBrowser browser, IPage page, ILogger logger)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogDebug("ImmobilienScout24: fetching page {Page}", pageNumber);

            var pageResult = await FetchPageInternalAsync(pageNumber, ct);

            if (pageResult == null)
            {
                yield break;
            }

            yield return pageResult;

            // Check if this was the last page
            if (_isLastPage)
            {
                _logger.LogInformation("ImmobilienScout24: pagination complete");
                yield break;
            }

            pageNumber++;
        }

        // Hard cap reached
        _logger.LogWarning("ImmobilienScout24: pagination reached the safety cap of {MaxPages} pages, stopping", MaxPagesLimit);
    }

    private async Task<string?> FetchPageInternalAsync(int pageNumber, CancellationToken ct)
    {
        try
        {
            // Click next page button to navigate to the target page
            _logger.LogDebug("ImmobilienScout24: page {Page}: clicking next page button", pageNumber);
            var nextButton = await _page.QuerySelectorAsync("button[data-testid='pagination-button-next']:not([disabled])");
            if (nextButton == null)
            {
                _logger.LogInformation("ImmobilienScout24: next page button not found or disabled, pagination complete");
                _isLastPage = true;
                return null;
            }

            await nextButton.ClickAsync();

            // Wait for the next page to load after clicking
            _logger.LogDebug("ImmobilienScout24: page {Page}: waiting for page load", pageNumber);
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60000 });
            await Task.Delay(2000); // Give it 2 more seconds for listings to render
            _logger.LogDebug("ImmobilienScout24: page {Page}: loaded, extracting HTML", pageNumber);

            // Extract current page HTML (now on the new page)
            var html = await _page.ContentAsync();
            _logger.LogDebug("ImmobilienScout24: page {Page}: HTML extracted ({Bytes} bytes)", pageNumber, html.Length);

            // Count listing cards on this page
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var listingCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'listing-card') and @data-obid]");
            int listingCount = listingCards?.Count ?? 0;
            if (listingCount == 0)
            {
                _logger.LogInformation("ImmobilienScout24: page {Page} has no listings, pagination complete", pageNumber);
                return null;
            }

            _logger.LogInformation("ImmobilienScout24: page {Page}: {Count} listings found", pageNumber, listingCount);
            return html;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImmobilienScout24: failed to fetch page {Page}, stopping pagination", pageNumber);
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
