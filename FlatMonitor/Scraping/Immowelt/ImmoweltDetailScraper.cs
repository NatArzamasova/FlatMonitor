namespace FlatMonitor.Scraping.Immowelt;

using System.Net;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Enriches a card with its expose page, opened by URL in the scraper's tab after
/// a pause of <see cref="ImmoweltOptions.NextPause"/>. Saves each expose through the <see cref="HtmlPageArchive"/>
/// before parsing it, so a page that fails to parse is kept. On DataDome's robot check it waits for the user to pass
/// it (<see cref="ImmoweltRobotCheck"/>). One instance per run: if the check is not passed, the run's remaining flats
/// keep their card data without opening their exposes.
/// </summary>
internal sealed class ImmoweltDetailScraper
{
    private const int NavigationTimeoutMs = 30_000;

    private readonly IPage _page;
    private readonly ImmoweltOptions _options;
    private readonly HtmlPageArchive _htmlPages;
    private readonly ILogger _logger;
    private readonly ImmoweltDetailParser _parser = new();
    private readonly ImmoweltRobotCheck _robotCheck;
    /// <summary>Set once DataDome blocks the exposes or the connection to Chrome is lost: no further expose would load.</summary>
    private bool _stopOpeningExposes;

    public ImmoweltDetailScraper(
        IPage page, ImmoweltOptions options, HtmlPageArchive htmlPages, ILogger logger, ImmoweltRobotCheck? robotCheck = null)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _htmlPages = htmlPages ?? throw new ArgumentNullException(nameof(htmlPages));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _robotCheck = robotCheck ?? new ImmoweltRobotCheck(page, options, logger);
    }

    /// <summary>
    /// The card with its expose's data; the card unchanged if it has no URL, DataDome blocks the exposes or the
    /// connection to Chrome is lost (from then on for all further cards, without opening their exposes);
    /// <c>null</c> if the listing is gone (HTTP 404/410, or the expose says it is no longer published). Throws if the
    /// expose cannot be loaded or has no data; <see cref="Common.FlatScraper"/> then keeps the card's data.
    /// </summary>
    public async Task<Flat?> EnrichAsync(Flat card, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(card.Url))
        {
            _logger.LogWarning("Immowelt: listing {ListingId} has no URL, keeping the card data", card.ListingId);
            return card;
        }

        if (_stopOpeningExposes)
            return card;

        // Every expose follows another page (a results page or the previous expose)
        await Task.Delay(_options.NextPause(), ct);

        _logger.LogDebug("Immowelt: listing {ListingId}: opening expose {Url}", card.ListingId, card.Url);
        var gotoOptions = new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = NavigationTimeoutMs };
        IResponse? response;
        string html;
        try
        {
            response = await _page.GotoAsync(card.Url, gotoOptions);
            ct.ThrowIfCancellationRequested();
            html = await _page.ContentAsync();
        }
        // Playwright reports a dropped connection as a closed page ("Target page, context or browser has been closed",
        // an internal TargetClosedException); the tab does not come back. Any other page error keeps only this card.
        catch (PlaywrightException ex) when (_page.IsClosed || ex.GetType().Name == "TargetClosedException")
        {
            _stopOpeningExposes = true;
            _logger.LogWarning(ex, "Immowelt: the connection to Chrome is lost (listing {ListingId}; Windows asleep, Chrome " +
                "closed?): the remaining flats keep their card data", card.ListingId);
            return card;
        }

        if (response?.Status is (int)HttpStatusCode.NotFound or (int)HttpStatusCode.Gone)
        {
            _logger.LogInformation("Immowelt: listing {ListingId}: expose gone (HTTP {Status}), left out", card.ListingId, response.Status);
            return null;
        }

        await _htmlPages.SaveAsync(card.ListingId, html, ct);

        var expose = _parser.Parse(html);
        if (expose == null)
        {
            if (!ImmoweltPageInspection.Inspect(html).IsDataDomeGuard)
                throw new InvalidOperationException($"Immowelt expose of listing {card.ListingId} has no data, has the page changed?");

            if (await _robotCheck.WaitForPassAsync(card.Url, _parser.Parse, gotoOptions, ct) is { } passed)
            {
                // Kept as any other expose
                await _htmlPages.SaveAsync(card.ListingId, passed.Html, ct);
                expose = passed.Data;
            }
        }

        if (expose == null)
        {
            _stopOpeningExposes = true;
            _logger.LogWarning("Immowelt: DataDome blocks the exposes (listing {ListingId}), the robot check was not passed: the " +
                "remaining flats keep their card data", card.ListingId);
            return card;
        }

        if (expose.IsDeactivated)
        {
            _logger.LogInformation("Immowelt: listing {ListingId} is no longer published, left out", card.ListingId);
            return null;
        }

        return expose.ApplyTo(card);
    }
}
