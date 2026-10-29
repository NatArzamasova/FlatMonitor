namespace FlatMonitor.Scraping.ImmobilienScout24;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

public class ImmobilienScout24BrowserScraper : IAsyncDisposable
{
    private readonly ILogger _logger;
    private IBrowser? _browser;
    private IPage? _page;

    public ImmobilienScout24BrowserScraper(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IBrowser? browser, IPage? page)> LaunchAndAuthenticateAsync()
    {
        try
        {
            var playwright = await Playwright.CreateAsync();

            _logger.LogInformation("ImmobilienScout24: launching browser");
            _browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });

            _page = await _browser.NewPageAsync();

            var listingsUrl = "https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen";
            _logger.LogInformation("ImmobilienScout24: navigating to {Url}", listingsUrl);
            await _page.GotoAsync(listingsUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            _logger.LogDebug("ImmobilienScout24: page URL after navigation: {Url}", _page.Url);

            // Wait for listing cards to appear automatically
            _logger.LogInformation("ImmobilienScout24: waiting for listings to load");
            try
            {
                await _page.WaitForSelectorAsync("div[data-obid], div[class*='listing'], div[class*='card-item']",
                    new PageWaitForSelectorOptions { Timeout = 60000 });
                _logger.LogDebug("ImmobilienScout24: listing cards found on page");
            }
            catch (PlaywrightException)
            {
                _logger.LogWarning("ImmobilienScout24: no listing cards detected after 60s, proceeding anyway");
            }

            // Wait for network to stabilize
            await Task.Delay(2000);

            _logger.LogInformation("ImmobilienScout24: browser opened and listings page loaded");

            return (_browser, _page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImmobilienScout24: failed to launch the browser or load the listings page");
            return (null, null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync();
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        GC.SuppressFinalize(this);
    }
}
