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

            // Wait for user to interact with the page (accept cookies, solve CAPTCHA, etc.)
            Console.WriteLine("\n⏳ Browser is open and waiting for your interaction.");
            Console.WriteLine("   Please:");
            Console.WriteLine("   1. Accept cookies if prompted");
            Console.WriteLine("   2. Solve any CAPTCHA/robot check");
            Console.WriteLine("   3. Press Enter here when done...\n");

            Console.ReadLine();

            Console.WriteLine("Continuing with scraping...");

            // Wait for listing cards to appear
            try
            {
                await _page.WaitForSelectorAsync("div[data-obid], div[class*='listing'], div[class*='card-item']",
                    new PageWaitForSelectorOptions { Timeout = 10000 });
                Console.WriteLine("✓ Listing cards found on page!");
            }
            catch (PlaywrightException)
            {
                Console.WriteLine("Warning: No listing cards detected. Page may still be loading.");
            }

            // Wait for network to stabilize
            await Task.Delay(2000);

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
