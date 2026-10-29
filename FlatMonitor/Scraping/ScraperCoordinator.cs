namespace FlatMonitor.Scraping;

using FlatMonitor.Models;
using FlatMonitor.Storage;

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

    public ScraperCoordinator(
        IEnumerable<IScraper> scrapers,
        ISnapshotRepository repository,
        SnapshotWriter snapshotWriter,
        string dataDirectory)
    {
        _scrapers = scrapers ?? throw new ArgumentNullException(nameof(scrapers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _snapshotWriter = snapshotWriter ?? throw new ArgumentNullException(nameof(snapshotWriter));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
    }

    /// <summary>
    /// Scrapes all sources. A failing source is recorded in the summary and the next source still runs;
    /// cancellation stops the whole run.
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
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Scraping canceled for {scraper.Source}");
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(HandleScraperError(scraper, ex));
            }
        }

        return new ScrapeSummary(flatsPerSource, errors);
    }

    private async Task ScrapeSourceAsync(IScraper scraper, Action<int> countSaved, Func<int, Task>? onBatchSaved, CancellationToken ct)
    {
        Console.WriteLine($"Starting scrape for source: {scraper.Source}");
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
                Console.WriteLine($"Snapshot written for {scraper.Source}: {snapshot.Path}");
            }
        }

        if (scraped == 0)
            Console.WriteLine($"No listings found for source: {scraper.Source}");
        else if (scraped > saved)
            Console.WriteLine($"Skipped {scraped - saved} listings without allowed districts");
    }

    /// <summary>Keeps flats in a tracked district, with the district name in its canonical spelling.</summary>
    private static List<Flat> FilterAndNormalizeBatch(IEnumerable<Flat> batch)
    {
        return batch
            .Select(flat => KarlsruheDistricts.Match(flat.District) is { } district ? flat with { District = district } : null)
            .OfType<Flat>()
            .ToList();
    }

    private static string HandleScraperError(IScraper scraper, Exception ex)
    {
        Console.Error.WriteLine($"Error scraping {scraper.Source}: {ex.Message}");
        if (Environment.GetEnvironmentVariable("DEBUG") == "1")
            Console.Error.WriteLine(ex.StackTrace);
        return $"{scraper.Source}: {ex.Message}";
    }
}
