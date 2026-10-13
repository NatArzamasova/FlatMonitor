namespace workInProgress.Scraping.ImmobilienScout24;

using Microsoft.Playwright;
using workInProgress.Models;

/// <summary>
/// Fetches and parses detail pages from ImmobilienScout24 expose URLs.
/// Uses the authenticated browser session to access detail pages.
/// </summary>
public class ImmobilienScout24DetailScraper
{
    private readonly IPage _page;

    public ImmobilienScout24DetailScraper(IPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>
    /// Fetch and parse detail page from the given expose URL.
    /// </summary>
    public async Task<Details?> FetchAndParseDetailPageAsync(string url)
    {
        try
        {
            Console.WriteLine($"Fetching detail page: {url}");
            await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await Task.Delay(1000); // Allow time for content to render

            var html = await _page.ContentAsync();
            Console.WriteLine($"Detail page fetched ({html.Length} bytes)");

            // Save for analysis (first page only)
            var debugFile = Path.Combine(Path.GetTempPath(), $"is24_detail_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");
            await File.WriteAllTextAsync(debugFile, html);
            Console.WriteLine($"Detail page saved to: {debugFile}");

            // Parse detail fields
            var parser = new ImmobilienScout24DetailParser();
            var details = parser.ParseDetailFields(html);

            if (details != null)
            {
                Console.WriteLine($"Parsed details: PropertyType={details.PropertyType}, BuildYear={details.BuildYear}, Heating={details.Heating}");
            }

            return details;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching detail page: {ex.Message}");
            return null;
        }
    }
}
