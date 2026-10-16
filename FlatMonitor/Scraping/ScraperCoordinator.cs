namespace workInProgress.Scraping;

using workInProgress.Models;
using workInProgress.Storage;

public sealed class ScraperCoordinator : IAsyncDisposable
{
    private readonly IEnumerable<IScraper> _scrapers;
    private readonly ISnapshotRepository _repository;
    private readonly SnapshotWriter _snapshotWriter;
    private readonly string _dataDirectory;

    public ScraperCoordinator(
        IEnumerable<IScraper> scrapers,
        ISnapshotRepository repository,
        SnapshotWriter snapshotWriter,
        string? dataDirectory = null)
    {
        _scrapers = scrapers ?? throw new ArgumentNullException(nameof(scrapers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _snapshotWriter = snapshotWriter ?? throw new ArgumentNullException(nameof(snapshotWriter));
        _dataDirectory = dataDirectory ?? StoragePaths.GetDefaultDataDirectory();
    }

    public async Task ScrapeAllSourcesAsync(CancellationToken ct = default)
    {
        await _repository.InitializeAsync(ct);

        foreach (var scraper in _scrapers)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                Console.WriteLine($"Starting scrape for source: {scraper.Source}");
                var listings = (await scraper.ScrapeAsync(ct)).ToList();

                ct.ThrowIfCancellationRequested();

                if (listings.Count == 0)
                {
                    Console.WriteLine($"No listings found for source: {scraper.Source}");
                    continue;
                }

                await _repository.UpsertListingsAsync(listings);

                var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(
                    listings,
                    baseDirectory: _dataDirectory,
                    source: scraper.Source,
                    ct: ct);

                Console.WriteLine($"Snapshot written for {scraper.Source}: {snapshotPath}");
                Console.WriteLine($"Database updated with {listings.Count} listings from {scraper.Source}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Scraping canceled for {scraper.Source}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error scraping {scraper.Source}: {ex.Message}");
                if (Environment.GetEnvironmentVariable("DEBUG") == "1")
                    Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _repository.DisposeAsync();
    }
}
