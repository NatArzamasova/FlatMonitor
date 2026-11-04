namespace FlatMonitor.Scraping.Immowelt;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Waits for the user to pass DataDome's robot check in the scraper's tab: brings the tab to the front, then reads it
/// every few seconds until it shows the page's data, for at most <see cref="ImmoweltOptions.RobotCheckWaitSeconds"/>.
/// The scraper never touches the check itself; it only leaves the tab open long enough for the user to solve it.
/// </summary>
internal sealed class ImmoweltRobotCheck
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    private readonly IPage _page;
    private readonly ImmoweltOptions _options;
    private readonly ILogger _logger;
    private readonly TimeSpan _pollInterval;

    /// <param name="pollInterval">How often the tab is read; <c>null</c>: every 2 s. Shorter in tests.</param>
    public ImmoweltRobotCheck(IPage page, ImmoweltOptions options, ILogger logger, TimeSpan? pollInterval = null)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    /// <summary>
    /// The tab's HTML and its data (<paramref name="parse"/> of it) once the user passed the check; <c>null</c> if waiting is
    /// off, the time is up or the tab was closed. When the check is gone but the tab shows no data (DataDome did not
    /// reload the page), opens <paramref name="url"/> again.
    /// </summary>
    public async Task<(T Data, string Html)?> WaitForPassAsync<T>(string url, Func<string, T?> parse, PageGotoOptions gotoOptions, CancellationToken ct)
        where T : class
    {
        if (_options.RobotCheckWaitSeconds == 0)
            return null;

        var wait = TimeSpan.FromSeconds(_options.RobotCheckWaitSeconds);
        _logger.LogWarning("Immowelt: DataDome shows its robot check ({Url}): pass it in the Chrome window, waiting up to {Wait}",
            url, wait);

        try
        {
            await _page.BringToFrontAsync();
        }
        catch (PlaywrightException)
        {
            // Only a convenience: the user finds the tab anyway
        }

        var waited = Stopwatch.StartNew();
        var reopened = false;
        while (waited.Elapsed < wait)
        {
            await Task.Delay(_pollInterval, ct);
            if (_page.IsClosed)
                break;

            string html;
            try
            {
                html = await _page.ContentAsync();
            }
            catch (PlaywrightException) when (!_page.IsClosed)
            {
                // The tab is navigating (the check reloads the page): read it on the next round
                continue;
            }

            var data = parse(html);
            if (data != null)
            {
                _logger.LogInformation("Immowelt: robot check passed after {Seconds:0} s, going on", waited.Elapsed.TotalSeconds);
                return (data, html);
            }

            if (ImmoweltPageInspection.Inspect(html).IsDataDomeGuard)
            {
                reopened = false;
            }
            else if (!reopened)
            {
                reopened = true;
                _logger.LogDebug("Immowelt: robot check gone, opening {Url} again", url);
                try
                {
                    await _page.GotoAsync(url, gotoOptions);
                }
                catch (PlaywrightException) when (!_page.IsClosed)
                {
                    // Not loaded (a timeout): read the tab as it is on the next round
                }
            }
        }

        _logger.LogWarning("Immowelt: the robot check was not passed within {Wait}", wait);
        return null;
    }
}
