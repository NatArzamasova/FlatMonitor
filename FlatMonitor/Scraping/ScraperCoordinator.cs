namespace FlatMonitor.Scraping;

using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs every scraper in turn and owns everything after scraping: district filtering, saving in batches,
/// progress reporting, and the JSONL snapshot. Scrapers only produce flats.
/// </summary>
public sealed class ScraperCoordinator
{
    private const int BatchSize = 20;

    private readonly IEnumerable<IScraper> _scrapers;
    private readonly ISnapshotRepository _repository;
    private readonly SnapshotWriter _snapshotWriter;
    private readonly string _dataDirectory;
    private readonly ILogger<ScraperCoordinator> _logger;

    public ScraperCoordinator(
        IEnumerable<IScraper> scrapers,
        ISnapshotRepository repository,
        SnapshotWriter snapshotWriter,
        string dataDirectory,
        ILogger<ScraperCoordinator> logger)
    {
        _scrapers = scrapers ?? throw new ArgumentNullException(nameof(scrapers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _snapshotWriter = snapshotWriter ?? throw new ArgumentNullException(nameof(snapshotWriter));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scrapes all sources. A failing source, or one that saved no flats, is recorded in the summary and the next
    /// source still runs; cancellation through <paramref name="ct"/> stops the whole run.
    /// </summary>
    /// <param name="onBatchSaved">
    /// Awaited after each saved batch with the number of flats saved (after district filtering).
    /// A callback rather than <see cref="IProgress{T}"/>: callers write it to the same DbContext, so it must finish before the next batch.
    /// </param>
    public async Task<ScrapeSummary> ScrapeAllSourcesAsync(Func<int, Task>? onBatchSaved = null, CancellationToken ct = default)
    {
        var flatsPerSource = new Dictionary<string, int>();
        var errors = new List<string>();

        foreach (var scraper in _scrapers)
        {
            ct.ThrowIfCancellationRequested();
            flatsPerSource[scraper.Source] = 0;

            try
            {
                await ScrapeSourceAsync(scraper, saved => flatsPerSource[scraper.Source] += saved, onBatchSaved, ct);

                // An empty source is a failure (blocked, changed markup), not an empty market
                if (flatsPerSource[scraper.Source] == 0)
                {
                    _logger.LogError("No flats found for source {Source}", scraper.Source);
                    errors.Add($"{scraper.Source}: no flats found");
                }
            }
            // Only the caller's token cancels the run; an HttpClient timeout is a TaskCanceledException too
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Scraping canceled for {Source}", scraper.Source);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping {Source}", scraper.Source);
                errors.Add($"{scraper.Source}: {ex.Message}");
            }
        }

        return new ScrapeSummary(flatsPerSource, errors);
    }

    private async Task ScrapeSourceAsync(IScraper scraper, Action<int> countSaved, Func<int, Task>? onBatchSaved, CancellationToken ct)
    {
        _logger.LogInformation("Starting scrape for source {Source}", scraper.Source);
        var scraped = 0;
        var saved = 0;
        SnapshotFile? snapshot = null;

        try
        {
            await foreach (var batch in scraper.ScrapeAsync(ct).Chunk(BatchSize))
            {
                scraped += batch.Length;
                var filtered = FilterAndNormalizeBatch(batch);
                if (filtered.Count == 0)
                    continue;

                await _repository.UpsertListingsAsync(filtered, ct);
                saved += filtered.Count;
                countSaved(filtered.Count);
                _logger.LogInformation("{Source}: saved {Count} flats ({Total} so far)", scraper.Source, filtered.Count, saved);
                if (onBatchSaved != null)
                    await onBatchSaved(filtered.Count);

                // Debugging backup only; written as the flats are saved, so a failed source keeps what it saved
                snapshot ??= _snapshotWriter.Create(_dataDirectory, scraper.Source);
                await snapshot.AppendAsync(filtered, ct);
            }
        }
        finally
        {
            if (snapshot != null)
            {
                await snapshot.DisposeAsync();
                _logger.LogDebug("Snapshot written for {Source}: {Path}", scraper.Source, snapshot.Path);
            }
        }

        if (scraped > saved)
            _logger.LogInformation("{Source}: skipped {Count} listings outside the tracked districts", scraper.Source, scraped - saved);
    }

    /// <summary>Keeps flats in a tracked district, with the district name in its canonical spelling.</summary>
    private static List<Flat> FilterAndNormalizeBatch(IEnumerable<Flat> batch)
    {
        return batch
            .Select(flat => KarlsruheDistricts.Match(flat.District) is { } district ? flat with { District = district } : null)
            .OfType<Flat>()
            .ToList();
    }
}
