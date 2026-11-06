namespace FlatMonitor.Scraping;

using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs all scrapers at the same time and owns everything after scraping: district filtering, saving in batches,
/// downloading the flats' title photos, progress reporting, and the JSONL backup file. Scrapers only produce flats.
/// Batches go into the repository's staging table; a source replaces its rows in FlatsSnapshots only once it has
/// finished with flats. Until then, and for good if it fails, the API keeps serving its rows of the previous run.
/// </summary>
public sealed class ScraperCoordinator
{
    private const int BatchSize = 20;

    /// <summary>Above this share of a source's flats without a district, the parser is likely broken, not the market odd.</summary>
    private const double MissingDistrictWarningShare = 0.1;

    /// <summary>Above this share of saved flats without price, area or rooms, the source's markup has likely changed.</summary>
    private const double MissingValueWarningShare = 0.2;

    private readonly IEnumerable<IScraper> _scrapers;
    private readonly IFlatRepository _repository;
    private readonly IFlatPreviewDownloader _previews;
    private readonly JsonlBackupWriter _backupWriter;
    private readonly ILogger<ScraperCoordinator> _logger;

    public ScraperCoordinator(
        IEnumerable<IScraper> scrapers,
        IFlatRepository repository,
        IFlatPreviewDownloader previews,
        JsonlBackupWriter backupWriter,
        ILogger<ScraperCoordinator> logger)
    {
        _scrapers = scrapers ?? throw new ArgumentNullException(nameof(scrapers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _previews = previews ?? throw new ArgumentNullException(nameof(previews));
        _backupWriter = backupWriter ?? throw new ArgumentNullException(nameof(backupWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The sources this coordinator can scrape, in registration order.</summary>
    public IReadOnlyList<string> Sources => _scrapers.Select(scraper => scraper.Source).ToList();

    /// <summary>
    /// Scrapes all sources at the same time and publishes each one that succeeded as soon as it has finished. They are
    /// different sites, each in its own browser or HttpClient; only their database work is taken one call at a time
    /// (<see cref="ScrapeDatabaseLock"/>). A failing source, or one that saved no flats, is recorded in the summary and
    /// keeps its previous rows; the other sources go on. Cancellation through <paramref name="ct"/> stops all of them
    /// (sources published before it stay published). Returns only once every source has stopped.
    /// </summary>
    /// <param name="onBatchSaved">
    /// Awaited after each saved batch with the number of flats saved (after district filtering), one call at a time.
    /// A callback rather than <see cref="IProgress{T}"/>: callers write it to the same DbContext, so it runs inside the
    /// database lock and must finish before the next database call.
    /// </param>
    /// <param name="scrapingHistoryId">The run (ScrapingHistories) whose published flats are archived (FlatsArchive).</param>
    /// <param name="sources">The sources to scrape, by <see cref="IScraper.Source"/>; <c>null</c>: all. The others keep their rows.</param>
    public async Task<ScrapeSummary> ScrapeAllSourcesAsync(
        int scrapingHistoryId,
        Func<int, Task>? onBatchSaved = null,
        CancellationToken ct = default,
        IReadOnlyCollection<string>? sources = null)
    {
        var scrapers = sources == null ? _scrapers : _scrapers.Where(scraper => sources.Contains(scraper.Source));
        var database = new ScrapeDatabaseLock();
        await _repository.BeginRunAsync(ct);
        SourceResult[] results;
        try
        {
            // Materialized first, so every source has started before the first one is awaited
            var running = scrapers.Select(scraper => ScrapeAndPublishAsync(scraper, scrapingHistoryId, database, onBatchSaved, ct)).ToList();
            results = await Task.WhenAll(running);
        }
        finally
        {
            // Task.WhenAll returns (or throws) only once every source has stopped, so nothing writes to staging any more
            await DiscardRunAsync();
        }

        // In registration order, whichever source finished first
        var flatsPerSource = new Dictionary<string, int>();
        foreach (var result in results)
            flatsPerSource[result.Source] = result.Saved;
        var errors = results.Select(result => result.Error).OfType<string>().ToList();
        return new ScrapeSummary(flatsPerSource, errors);
    }

    /// <summary>What one source's part of a run gave: the flats it saved, and the error if it failed.</summary>
    private sealed record SourceResult(string Source, int Saved, string? Error);

    /// <summary>Scrapes one source and publishes it; any failure but cancellation is returned, not thrown.</summary>
    private async Task<SourceResult> ScrapeAndPublishAsync(
        IScraper scraper, int scrapingHistoryId, ScrapeDatabaseLock database, Func<int, Task>? onBatchSaved, CancellationToken ct)
    {
        // Lets every source start before the first one runs synchronously up to its first await
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        var saved = 0;

        try
        {
            await ScrapeSourceAsync(scraper, database, count => saved += count, onBatchSaved, ct);

            // An empty source is a failure (blocked, changed markup), not an empty market
            if (saved == 0)
            {
                _logger.LogError("No flats found for source {Source}, keeping its previous flats", scraper.Source);
                return new SourceResult(scraper.Source, saved, $"{scraper.Source}: no flats found");
            }

            var published = await database.RunAsync(() => _repository.PublishSourceAsync(scraper.Source, scrapingHistoryId, ct), ct);
            _logger.LogInformation("{Source}: published {Count} flats", scraper.Source, published);
            return new SourceResult(scraper.Source, saved, Error: null);
        }
        // Only the caller's token cancels the run; an HttpClient timeout is a TaskCanceledException too
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Scraping canceled for {Source}", scraper.Source);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping {Source}, keeping its previous flats", scraper.Source);
            return new SourceResult(scraper.Source, saved, $"{scraper.Source}: {ex.Message}");
        }
    }

    /// <summary>Empties the staging table; a failure is only logged, so the run's own exception still propagates.</summary>
    private async Task DiscardRunAsync()
    {
        try
        {
            await _repository.DiscardRunAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Harmless: the next run starts by emptying it again
            _logger.LogWarning(ex, "Could not empty the staging table");
        }
    }

    private async Task ScrapeSourceAsync(
        IScraper scraper, ScrapeDatabaseLock database, Action<int> countSaved, Func<int, Task>? onBatchSaved, CancellationToken ct)
    {
        _logger.LogInformation("Starting scrape for source {Source}", scraper.Source);
        var scraped = 0;
        var withoutDistrict = 0;
        var saved = 0;
        var health = new ParseHealth();
        JsonlBackupFile? backup = null;

        try
        {
            await foreach (var batch in scraper.ScrapeAsync(ct).Chunk(BatchSize))
            {
                scraped += batch.Length;
                withoutDistrict += batch.Count(flat => string.IsNullOrWhiteSpace(flat.District));
                var filtered = FilterAndNormalizeBatch(batch, scraper.Source);
                if (filtered.Count == 0)
                    continue;

                await database.RunAsync(() => _repository.AddToRunAsync(filtered, ct), ct);
                // The title photos for the hover preview; a photo that fails leaves the flat without one
                await _previews.SavePreviewsAsync(filtered, database, ct);
                saved += filtered.Count;
                health.Count(filtered);
                countSaved(filtered.Count);
                _logger.LogInformation("{Source}: saved {Count} flats ({Total} so far)", scraper.Source, filtered.Count, saved);
                if (onBatchSaved != null)
                    await database.RunAsync(() => onBatchSaved(filtered.Count), ct);

                // Debugging backup only; written as the flats are staged, so a failed source's file keeps what it scraped
                backup ??= _backupWriter.Create(scraper.Source);
                await backup.AppendAsync(filtered, ct);
            }
        }
        finally
        {
            if (backup != null)
            {
                await backup.DisposeAsync();
                _logger.LogDebug("JSONL backup written for {Source}: {Path}", scraper.Source, backup.Path);
            }
        }

        LogSkipped(scraper.Source, scraped, withoutDistrict, outsideTrackedDistricts: scraped - saved - withoutDistrict);
        LogParseHealth(scraper.Source, saved, health);
    }

    /// <summary>
    /// One line per source on how many saved flats lack the values the aggregations need. The parsers return
    /// <c>null</c> for anything they cannot find, so a markup change shows up here rather than as an error.
    /// </summary>
    private void LogParseHealth(string source, int saved, ParseHealth health)
    {
        if (saved == 0)
            return;

        var limit = saved * MissingValueWarningShare;
        var level = health.WithoutPrice > limit || health.WithoutArea > limit || health.WithoutRooms > limit
            ? LogLevel.Warning
            : LogLevel.Information;
        _logger.Log(level, "{Source}: parse health: of {Saved} saved flats, {WithoutPrice} without price, {WithoutArea} without area, {WithoutRooms} without rooms",
            source, saved, health.WithoutPrice, health.WithoutArea, health.WithoutRooms);
    }

    private sealed class ParseHealth
    {
        public int WithoutPrice { get; private set; }
        public int WithoutArea { get; private set; }
        public int WithoutRooms { get; private set; }

        public void Count(IReadOnlyCollection<Flat> flats)
        {
            WithoutPrice += flats.Count(f => f.PriceEur is null);
            WithoutArea += flats.Count(f => f.LivingAreaSqm is null);
            WithoutRooms += flats.Count(f => f.RoomsCount is null);
        }
    }

    /// <summary>
    /// Flats without a district are counted apart from flats in an untracked district: they usually mean a detail page
    /// failed or its markup changed, which would otherwise look like a quiet market.
    /// </summary>
    private void LogSkipped(string source, int scraped, int withoutDistrict, int outsideTrackedDistricts)
    {
        if (outsideTrackedDistricts > 0)
            _logger.LogInformation("{Source}: skipped {Count} listings outside the tracked districts", source, outsideTrackedDistricts);

        if (withoutDistrict == 0)
            return;

        var level = withoutDistrict > scraped * MissingDistrictWarningShare ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(level, "{Source}: skipped {Count} of {Scraped} listings without a district", source, withoutDistrict, scraped);
    }

    /// <summary>
    /// Keeps flats in a tracked district, with the district name in its canonical spelling. Every flat gets the
    /// scraper's source: the source is published by that name, so a flat with another one would never be published.
    /// </summary>
    private static List<Flat> FilterAndNormalizeBatch(IEnumerable<Flat> batch, string source)
    {
        return batch
            .Select(flat => KarlsruheDistricts.Match(flat.District) is { } district ? flat with { District = district, Source = source } : null)
            .OfType<Flat>()
            .ToList();
    }
}
