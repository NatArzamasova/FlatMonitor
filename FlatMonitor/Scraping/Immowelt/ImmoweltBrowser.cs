namespace FlatMonitor.Scraping.Immowelt;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// The scraper's tab in the user's Chrome. Disposing it closes only that tab and disconnects; the browser and its
/// other tabs stay open, they are the user's.
/// </summary>
public sealed class ImmoweltBrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;

    public ImmoweltBrowserSession(IPlaywright playwright, IPage page)
    {
        _playwright = playwright;
        Page = page;
    }

    public IPage Page { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Page.CloseAsync();
        }
        finally
        {
            // Disconnects from the Chrome without closing it
            _playwright.Dispose();
        }
    }
}

/// <summary>
/// Attaches to the Chrome the user started with <c>--remote-debugging-port</c> (<see cref="ImmoweltOptions.CdpUrl"/>)
/// and opens a tab in it. Playwright's own browser is no option: DataDome blocks it, even headed.
/// </summary>
public class ImmoweltBrowser
{
    /// <summary>Connecting to a running Chrome is quick; a missing one is refused at once anyway.</summary>
    private const int ConnectTimeoutMs = 10_000;

    private readonly ImmoweltOptions _options;
    private readonly ILogger _logger;

    public ImmoweltBrowser(ImmoweltOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attaches to the Chrome and opens a new tab. Throws <see cref="InvalidOperationException"/> with what to do if
    /// there is no Chrome to attach to; nothing is left open then, nor if <paramref name="ct"/> is canceled.
    /// </summary>
    public async Task<ImmoweltBrowserSession> AttachAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var playwright = await Playwright.CreateAsync();

        try
        {
            // Playwright takes no token: check between the steps
            _logger.LogInformation("Immowelt: attaching to the running Chrome at {CdpUrl}", _options.CdpUrl);
            var browser = await ConnectAsync(playwright);

            ct.ThrowIfCancellationRequested();
            var context = browser.Contexts.FirstOrDefault()
                ?? throw new InvalidOperationException($"The Chrome at {_options.CdpUrl} has no open window.");
            var page = await context.NewPageAsync();

            return new ImmoweltBrowserSession(playwright, page);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    private async Task<IBrowser> ConnectAsync(IPlaywright playwright)
    {
        try
        {
            return await playwright.Chromium.ConnectOverCDPAsync(
                _options.CdpUrl, new BrowserTypeConnectOverCDPOptions { Timeout = ConnectTimeoutMs });
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            throw new InvalidOperationException(NoChromeMessage(_options.CdpUrl), ex);
        }
    }

    public static string NoChromeMessage(string cdpUrl)
    {
        var port = Uri.TryCreate(cdpUrl, UriKind.Absolute, out var uri) ? uri.Port : 9222;
        return $"No Chrome to attach to at {cdpUrl}. Start Chrome with --remote-debugging-port={port} " +
               "and its own profile (--user-data-dir=\"%LOCALAPPDATA%\\FlatMonitorChrome\"), " +
               "open immowelt.de once and pass its robot check, then scrape again.";
    }
}
