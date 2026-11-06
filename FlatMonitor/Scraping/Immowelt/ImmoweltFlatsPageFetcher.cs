namespace FlatMonitor.Scraping.Immowelt;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Opens the search's results pages one after the other by URL (<see cref="ImmoweltPages.PageUrl"/>) and parses
/// them. The tab belongs to the scraper's <see cref="ImmoweltBrowserSession"/>.
/// </summary>
internal sealed class ImmoweltFlatsPageFetcher
{
    /// <summary>immowelt shows 30 flats per results page.</summary>
    public const int PageSize = 30;

    /// <summary>A safety cap, should the total count be wrong: 50 pages are 1,500 flats, Karlsruhe has ~270.</summary>
    public const int MaxPages = 50;

    private const int NavigationTimeoutMs = 60_000;

    private readonly IPage _page;
    private readonly ImmoweltOptions _options;
    private readonly ImmoweltListParser _parser;
    private readonly ImmoweltRobotCheck _robotCheck;
    private readonly ILogger _logger;

    public ImmoweltFlatsPageFetcher(IPage page, ImmoweltOptions options, ILogger logger, ImmoweltRobotCheck? robotCheck = null)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = new ImmoweltListParser(logger);
        _robotCheck = robotCheck ?? new ImmoweltRobotCheck(page, options, logger);
    }

    /// <summary>
    /// Yields results page 1, then pages 2..⌈totalCount / <see cref="PageSize"/>⌉ (at most <see cref="MaxPages"/>),
    /// with a pause of <see cref="ImmoweltOptions.NextPause"/> between two pages. On DataDome's robot check it waits for
    /// the user to pass it (<see cref="ImmoweltRobotCheck"/>). A page without search data (the check not passed, a
    /// changed page) throws, on page 1 as on a later page: stopping early would look like a successful scrape with
    /// fewer flats.
    /// </summary>
    public async IAsyncEnumerable<ImmoweltListPage> FetchAllPagesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var firstPage = await FetchPageAsync(1, ct);
        yield return firstPage;

        var lastPage = LastPage(firstPage.TotalCount);
        if (lastPage == MaxPages)
            _logger.LogWarning("Immowelt: {TotalCount} flats found, reading at most {MaxPages} pages", firstPage.TotalCount, MaxPages);

        _logger.LogInformation("Immowelt: {TotalCount} flats found, {Pages} results pages", firstPage.TotalCount, lastPage);

        for (var pageNumber = 2; pageNumber <= lastPage; pageNumber++)
        {
            await Task.Delay(_options.NextPause(), ct);
            yield return await FetchPageAsync(pageNumber, ct);
        }
    }

    /// <summary>The last results page of a search with <paramref name="totalCount"/> flats: 1 to <see cref="MaxPages"/>.</summary>
    internal static int LastPage(int totalCount) => Math.Clamp((totalCount + PageSize - 1) / PageSize, 1, MaxPages);

    private async Task<ImmoweltListPage> FetchPageAsync(int pageNumber, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var url = ImmoweltPages.PageUrl(_options.SearchUrl, pageNumber);
        _logger.LogDebug("Immowelt: opening results page {Page}: {Url}", pageNumber, url);

        // The data script is in the server-rendered HTML: no need to wait for the page's scripts and trackers
        var gotoOptions = new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = NavigationTimeoutMs };
        var response = await _page.GotoAsync(url, gotoOptions);
        ct.ThrowIfCancellationRequested();

        var html = await _page.ContentAsync();
        var page = _parser.Parse(html);
        var blocked = page == null && ImmoweltPageInspection.Inspect(html).IsDataDomeGuard;
        if (blocked)
            page = (await _robotCheck.WaitForPassAsync(url, _parser.Parse, gotoOptions, ct))?.Data;

        if (page == null)
        {
            var reason = blocked
                ? "blocked by DataDome, the robot check was not passed in the attached Chrome " +
                  $"(waited {_options.RobotCheckWaitSeconds} s, {ImmoweltOptions.SectionName}:{nameof(ImmoweltOptions.RobotCheckWaitSeconds)})"
                : "no search data, has the page changed?";
            throw new InvalidOperationException(
                $"Immowelt results page {pageNumber} (HTTP {response?.Status.ToString() ?? "?"}): {reason}; " +
                await ImmoweltDebugPage.SaveAsync(_options, html, $"page{pageNumber}", ct));
        }

        _logger.LogDebug("Immowelt: results page {Page}: {Count} flats", pageNumber, page.Flats.Count);
        return page;
    }
}
