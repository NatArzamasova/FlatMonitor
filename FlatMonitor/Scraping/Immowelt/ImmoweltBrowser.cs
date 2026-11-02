namespace FlatMonitor.Scraping.Immowelt;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// The scraper's tab in the user's Chrome. Disposing it closes that tab and disconnects. The browser itself is closed
/// only if the scraper started it; a Chrome the user started stays open with its other tabs.
/// </summary>
public sealed class ImmoweltBrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser? _browserToClose;

    /// <param name="browserToClose">The browser to close when the session ends, if the scraper started it.</param>
    public ImmoweltBrowserSession(IPlaywright playwright, IPage page, IBrowser? browserToClose = null)
    {
        _playwright = playwright;
        Page = page;
        _browserToClose = browserToClose;
    }

    public IPage Page { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Page.CloseAsync();
            if (_browserToClose != null)
                await ImmoweltBrowser.CloseChromeAsync(_browserToClose);
        }
        catch (PlaywrightException)
        {
            // The connection is already gone (e.g. Windows slept): nothing left to close from here, and a finished
            // run must not fail for it
        }
        finally
        {
            // Disconnects from the Chrome
            _playwright.Dispose();
        }
    }
}

/// <summary>
/// Attaches to the Chrome listening at <see cref="ImmoweltOptions.CdpUrl"/> and opens a tab in it; if there is none,
/// starts one first (<see cref="ImmoweltOptions.LaunchChrome"/>). Playwright's own browser is no option: DataDome
/// blocks it, even headed.
/// </summary>
public class ImmoweltBrowser
{
    /// <summary>Connecting to a running Chrome is quick; a missing one is refused at once anyway.</summary>
    private const int ConnectTimeoutMs = 10_000;

    /// <summary>For asking Chrome whether it listens: a local call, answered at once or refused.</summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    private readonly ImmoweltOptions _options;
    private readonly ILogger _logger;

    public ImmoweltBrowser(ImmoweltOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attaches to the Chrome (starting it if needed and allowed) and opens a new tab. Throws
    /// <see cref="InvalidOperationException"/> with what to do if there is no Chrome to attach to; no tab is left open
    /// then, nor if <paramref name="ct"/> is canceled.
    /// </summary>
    public async Task<ImmoweltBrowserSession> AttachAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var started = await EnsureChromeAsync(ct);
        var playwright = await Playwright.CreateAsync();
        IBrowser? browser = null;

        try
        {
            // Playwright takes no token: check between the steps
            _logger.LogInformation("Immowelt: attaching to the running Chrome at {CdpUrl}", _options.CdpUrl);
            browser = await ConnectAsync(playwright);

            ct.ThrowIfCancellationRequested();
            var context = browser.Contexts.FirstOrDefault()
                ?? throw new InvalidOperationException($"The Chrome at {_options.CdpUrl} has no open window.");
            var page = await context.NewPageAsync();

            return new ImmoweltBrowserSession(playwright, page, started ? browser : null);
        }
        catch
        {
            try
            {
                if (started && browser != null)
                    await CloseChromeAsync(browser);
            }
            finally
            {
                playwright.Dispose();
            }
            throw;
        }
    }

    /// <summary>Closes the whole browser (all its windows), the way the user would; not just the connection.</summary>
    internal static async Task CloseChromeAsync(IBrowser browser)
    {
        try
        {
            var cdp = await browser.NewBrowserCDPSessionAsync();
            await cdp.SendAsync("Browser.close");
        }
        catch (PlaywrightException)
        {
            // Chrome closes the connection while answering
        }
    }

    /// <summary>
    /// <c>true</c> if no Chrome listened and this started one; <c>false</c> if one already listens. Throws if none listens
    /// and starting one is off.
    /// </summary>
    private async Task<bool> EnsureChromeAsync(CancellationToken ct)
    {
        var chrome = new ImmoweltChrome(_options, Http, _logger);
        if (await chrome.IsListeningAsync(ct))
            return false;

        if (!_options.LaunchChrome)
            throw new InvalidOperationException(NoChromeMessage(_options.CdpUrl));

        await chrome.StartAsync(ct);
        return true;
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

    public static string NoChromeMessage(string cdpUrl) =>
        $"No Chrome to attach to at {cdpUrl}. Start Chrome with --remote-debugging-port={ImmoweltChrome.Port(cdpUrl)} " +
        "and its own profile (--user-data-dir=\"%LOCALAPPDATA%\\FlatMonitorChrome\"), " +
        "open immowelt.de once and pass its robot check, then scrape again " +
        $"(or set {ImmoweltOptions.SectionName}:{nameof(ImmoweltOptions.LaunchChrome)} to true to have it started).";
}
