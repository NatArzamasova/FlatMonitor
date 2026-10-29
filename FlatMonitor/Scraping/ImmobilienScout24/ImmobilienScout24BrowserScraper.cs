namespace FlatMonitor.Scraping.ImmobilienScout24;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>The browser the scraper works in. Disposing it closes the page and the browser, then stops Playwright.</summary>
public sealed class ImmobilienScout24BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;

    public ImmobilienScout24BrowserSession(IPlaywright playwright, IBrowser browser, IPage page)
    {
        _playwright = playwright;
        Browser = browser;
        Page = page;
    }

    public IBrowser Browser { get; }
    public IPage Page { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Page.CloseAsync();
            await Browser.CloseAsync();
        }
        finally
        {
            _playwright.Dispose();
        }
    }
}

public class ImmobilienScout24BrowserScraper
{
    /// <summary>Any of these means the listing cards have rendered.</summary>
    private const string ListingCardSelector = "div[data-obid], div[class*='listing'], div[class*='card-item']";

    private readonly ImmobilienScout24Options _options;
    private readonly ILogger _logger;

    public ImmobilienScout24BrowserScraper(ImmobilienScout24Options options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Launches the browser and opens the first listings page. Throws if that fails; nothing is left open then.
    /// </summary>
    public async Task<ImmobilienScout24BrowserSession> LaunchAndAuthenticateAsync()
    {
        var playwright = await Playwright.CreateAsync();
        IBrowser? browser = null;

        try
        {
            _logger.LogInformation("ImmobilienScout24: launching browser (headless: {Headless})", _options.Headless);
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = _options.Headless });

            var page = await browser.NewPageAsync();

            _logger.LogInformation("ImmobilienScout24: navigating to {Url}", _options.SearchUrl);
            await page.GotoAsync(_options.SearchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            _logger.LogDebug("ImmobilienScout24: page URL after navigation: {Url}", page.Url);

            // Wait for listing cards to appear automatically
            _logger.LogInformation("ImmobilienScout24: waiting for listings to load");
            try
            {
                await page.WaitForSelectorAsync(ListingCardSelector, new PageWaitForSelectorOptions { Timeout = 60000 });
                _logger.LogDebug("ImmobilienScout24: listing cards found on page");
            }
            catch (PlaywrightException)
            {
                _logger.LogWarning("ImmobilienScout24: no listing cards detected after 60s, proceeding anyway");
            }

            _logger.LogInformation("ImmobilienScout24: browser opened and listings page loaded");

            return new ImmobilienScout24BrowserSession(playwright, browser, page);
        }
        catch
        {
            try
            {
                // Closing the browser also closes its page
                if (browser != null)
                    await browser.CloseAsync();
            }
            finally
            {
                playwright.Dispose();
            }
            throw;
        }
    }
}
