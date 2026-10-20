namespace workInProgress.Scraping.ImmobilienScout24;

using Microsoft.Playwright;

public class ImmobilienScout24BrowserScraper : IAsyncDisposable
{
    private IBrowser? _browser;
    private IPage? _page;

    public async Task<(IBrowser? browser, IPage? page)> LaunchAndAuthenticateAsync()
    {
        try
        {
            var playwright = await Playwright.CreateAsync();

            Console.WriteLine("Launching browser...");
            _browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });

            _page = await _browser.NewPageAsync();

            var listingsUrl = "https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen";
            Console.WriteLine($"Navigating to {listingsUrl}...");
            await _page.GotoAsync(listingsUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            Console.WriteLine($"Page URL after navigation: {_page.Url}");

            // Wait for listing cards to appear automatically
            Console.WriteLine("\n⏳ Waiting for listings to load (auto-detecting when ready)...");
            try
            {
                await _page.WaitForSelectorAsync("div[data-obid], div[class*='listing'], div[class*='card-item']",
                    new PageWaitForSelectorOptions { Timeout = 60000 });
                Console.WriteLine("✓ Listing cards found on page!");
            }
            catch (PlaywrightException)
            {
                Console.WriteLine("Warning: No listing cards detected after 60s. Proceeding anyway...");
            }

            // Wait for network to stabilize
            await Task.Delay(2000);

            Console.WriteLine("✓ Listings are ready, continuing with scraping...");

            Console.WriteLine("✓ Browser opened and page loaded.");

            return (_browser, _page);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
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
