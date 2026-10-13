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

            Console.WriteLine("\n✓ Browser opened and page loaded.");
            Console.WriteLine("\nPlease:");
            Console.WriteLine("  1. Log in with your Google account");
            Console.WriteLine("  2. Complete any robot guard verification");
            Console.WriteLine("  3. When logged in and the listings are visible, press Enter here...\n");

            Console.ReadLine();

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
    }
}
