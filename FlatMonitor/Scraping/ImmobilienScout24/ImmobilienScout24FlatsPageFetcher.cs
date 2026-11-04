namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches flats pages from immobilienscout24.de using Playwright browser automation.
/// Handles pagination by detecting and clicking the next-page button until no more pages exist.
/// The page and browser belong to the scraper's <see cref="ImmobilienScout24BrowserSession"/>.
/// </summary>
internal sealed class ImmobilienScout24FlatsPageFetcher
{
    private const int MaxPagesLimit = 50;

    /// <summary>A visible, enabled button is clicked at once; longer means something covers it.</summary>
    internal const int NextButtonClickTimeoutMs = 10_000;

    // True once the first listing card is no longer the one shown before clicking "next"
    private const string FirstCardChangedScript = """
        ([selector, previousId]) => {
            const card = document.querySelector(selector);
            return card !== null && card.getAttribute('data-obid') !== previousId;
        }
        """;

    private readonly IPage _page;
    private readonly ILogger _logger;

    public ImmobilienScout24FlatsPageFetcher(IPage page, ILogger logger)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Yields raw HTML for all pages, starting from page 2 onwards.
    /// Assumes the browser is already on page 1; fetches pages 2, 3, etc.
    /// Stops when the next-page button is disabled, when the caller stops reading (the session stops at a page
    /// without cards), or after reaching the safety limit.
    /// Any other failure throws: stopping early would look like a successful scrape with fewer flats.
    /// </summary>
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
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
            pageNumber++;
        }

        // Hard cap reached
        _logger.LogWarning("ImmobilienScout24: pagination reached the safety cap of {MaxPages} pages, stopping", MaxPagesLimit);
    }

    private async Task<string?> FetchPageInternalAsync(int pageNumber, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Click next page button to navigate to the target page
        _logger.LogDebug("ImmobilienScout24: page {Page}: clicking next page button", pageNumber);
        var nextButton = await _page.QuerySelectorAsync(ImmobilienScout24Selectors.NextPageButtonCss);
        if (nextButton == null)
        {
            _logger.LogInformation("ImmobilienScout24: next page button not found or disabled, pagination complete");
            return null;
        }

        var previousFirstCard = await _page.QuerySelectorAsync(ImmobilienScout24Selectors.ListingCardCss);
        var previousFirstId = previousFirstCard == null ? null : await previousFirstCard.GetAttributeAsync("data-obid");

        await ClickNextButtonAsync(nextButton, pageNumber);

        // Wait until the next page's listings have rendered
        _logger.LogDebug("ImmobilienScout24: page {Page}: waiting for page load", pageNumber);
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60000 });
        await _page.WaitForFunctionAsync(FirstCardChangedScript, new object?[] { ImmobilienScout24Selectors.ListingCardCss, previousFirstId },
            new PageWaitForFunctionOptions { Timeout = 60000 });
        _logger.LogDebug("ImmobilienScout24: page {Page}: loaded, extracting HTML", pageNumber);

        // Extract current page HTML (now on the new page)
        var html = await _page.ContentAsync();
        _logger.LogDebug("ImmobilienScout24: page {Page}: HTML extracted ({Bytes} bytes)", pageNumber, html.Length);
        return html;
    }

    /// <summary>
    /// Clicks "next page". If something covers the button (the cookie banner), denies the cookies in the banner and
    /// clicks again. Throws a short <see cref="InvalidOperationException"/> if the button still cannot be clicked:
    /// Playwright's own message lists every retry.
    /// </summary>
    private async Task ClickNextButtonAsync(IElementHandle nextButton, int pageNumber)
    {
        var clickOptions = new ElementHandleClickOptions { Timeout = NextButtonClickTimeoutMs };
        try
        {
            await nextButton.ClickAsync(clickOptions);
            return;
        }
        catch (TimeoutException ex)
        {
            if (!await DenyCookiesAsync())
                throw NotClickable(pageNumber, ex);
        }

        try
        {
            await nextButton.ClickAsync(clickOptions);
        }
        catch (TimeoutException ex)
        {
            throw NotClickable(pageNumber, ex);
        }
    }

    /// <summary>Clicks "deny all" in the cookie banner; <c>false</c> if there is no such button.</summary>
    private async Task<bool> DenyCookiesAsync()
    {
        var denyButton = await _page.QuerySelectorAsync(ImmobilienScout24Selectors.CookieBannerDenyButtonCss);
        if (denyButton == null)
            return false;

        _logger.LogInformation("ImmobilienScout24: the cookie banner covers the page, denying the cookies");
        try
        {
            await denyButton.ClickAsync(new ElementHandleClickOptions { Timeout = NextButtonClickTimeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static InvalidOperationException NotClickable(int pageNumber, TimeoutException ex)
    {
        // Only Playwright's reason, not its call log of every retry
        var reason = ex.Message.Contains("usercentrics", StringComparison.OrdinalIgnoreCase)
            ? "the cookie banner (usercentrics-root) covers it and could not be closed"
            : "something covers it or the page does not react";
        return new InvalidOperationException(
            $"ImmobilienScout24 page {pageNumber}: the next page button cannot be clicked: {reason} " +
            $"(after {NextButtonClickTimeoutMs / 1000} s)");
    }
}
