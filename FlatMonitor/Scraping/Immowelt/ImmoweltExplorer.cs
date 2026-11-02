namespace FlatMonitor.Scraping.Immowelt;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// The <c>explore-immowelt</c> spike: opens the search in a browser, saves the first two listings pages and the first
/// expose page, and reports what each contains (DataDome guard, expose links, embedded data). The saved pages are the
/// material for the parsers and their test fixtures. Not part of a scrape.
/// </summary>
public sealed class ImmoweltExplorer
{
    public const string SearchUrl =
        "https://www.immowelt.de/classified-search?distributionTypes=Buy,Buy_Auction,Compulsory_Auction&estateTypes=Apartment&locations=AD08DE5585";

    /// <summary>Waiting for the listings: long enough to solve a captcha by hand in a headed browser.</summary>
    private const int ExposeLinksTimeoutMs = 120_000;
    private const string ExposeLinkSelector = "a[href*='/expose/']";

    /// <summary>Pages opened back to back look like a robot to DataDome ("übermenschliche Geschwindigkeit").</summary>
    private const int PauseBetweenPagesMs = 5_000;

    /// <summary>
    /// Where a Chrome started with <c>--remote-debugging-port=9222</c> listens. Not <c>localhost</c>: that resolves to
    /// IPv6 <c>::1</c>, but Chrome listens on IPv4 only.
    /// </summary>
    public const string DefaultCdpUrl = "http://127.0.0.1:9222";

    private readonly string _outputDirectory;
    private readonly bool _headless;
    private readonly string? _cdpUrl;
    private readonly ILogger _logger;

    /// <param name="cdpUrl">
    /// Attach to this already running Chrome instead of launching Playwright's Chromium, which DataDome blocks even
    /// headed. <paramref name="headless"/> is ignored then.
    /// </param>
    public ImmoweltExplorer(string outputDirectory, bool headless, string? cdpUrl, ILogger logger)
    {
        _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        _headless = headless;
        _cdpUrl = cdpUrl;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Explores the search and its first expose; <c>false</c> if the browser never got past DataDome.</summary>
    public async Task<bool> ExploreAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_outputDirectory);

        using var playwright = await Playwright.CreateAsync();
        if (_cdpUrl == null)
        {
            _logger.LogInformation("Immowelt: launching browser (headless: {Headless})", _headless);
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = _headless });
            return await ExploreInAsync(await browser.NewPageAsync(new() { Locale = "de-DE" }), ct);
        }

        // Not closed afterwards: the browser is the user's. Disposing Playwright only disconnects from it.
        _logger.LogInformation("Immowelt: attaching to the running Chrome at {CdpUrl}", _cdpUrl);
        var userBrowser = await playwright.Chromium.ConnectOverCDPAsync(_cdpUrl);
        var context = userBrowser.Contexts.FirstOrDefault()
            ?? throw new InvalidOperationException($"The Chrome at {_cdpUrl} has no open window");
        var page = await context.NewPageAsync();
        try
        {
            return await ExploreInAsync(page, ct);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<bool> ExploreInAsync(IPage page, CancellationToken ct)
    {
        var firstPage = await OpenAndSaveAsync(page, SearchUrl, "list_page1", waitForExposeLinks: true, ct);
        if (firstPage.IsDataDomeGuard || firstPage.ExposeUrls.Count == 0)
        {
            _logger.LogWarning("Immowelt: no listings on the first page (blocked by DataDome?), stopping");
            return false;
        }

        await Task.Delay(PauseBetweenPagesMs, ct);
        await OpenAndSaveAsync(page, SearchUrl + "&page=2", "list_page2", waitForExposeLinks: true, ct);
        await Task.Delay(PauseBetweenPagesMs, ct);
        await OpenAndSaveAsync(page, firstPage.ExposeUrls[0], "expose", waitForExposeLinks: false, ct);
        return true;
    }

    private async Task<ImmoweltPageInspection> OpenAndSaveAsync(
        IPage page, string url, string name, bool waitForExposeLinks, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _logger.LogInformation("Immowelt: opening {Url}", url);
        var response = await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 });

        // A page that never gets there (DataDome, endless trackers) is still saved: it is what the scraper would see
        try
        {
            if (waitForExposeLinks)
                await page.WaitForSelectorAsync(ExposeLinkSelector,
                    new() { State = WaitForSelectorState.Attached, Timeout = ExposeLinksTimeoutMs });
            else
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Immowelt: {Name} did not finish loading in time, saving the page as it is", name);
        }

        var html = await page.ContentAsync();
        var path = Path.Combine(_outputDirectory, $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");
        await File.WriteAllTextAsync(path, html, ct);

        var inspection = ImmoweltPageInspection.Inspect(html);
        _logger.LogInformation("Immowelt: {Name} (HTTP {Status}, now at {Url}): {Inspection}; saved to {Path}",
            name, response?.Status, page.Url, inspection, path);
        return inspection;
    }
}
