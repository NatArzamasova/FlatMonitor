namespace workInProgress.Scraping;

using workInProgress.Models;
using workInProgress.Storage;

public sealed class ScraperCoordinator : IAsyncDisposable
{
    private static readonly HashSet<string> AllowedDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Daxlanden",
        "Durlach",
        "Grötzingen",
        "Grünwettersbach",
        "Grünwinkel",
        "Hagsfeld",
        "Hohenwettersbach",
        "Innenstadt-Ost",
        "Innenstadt-West",
        "Knielingen",
        "Mühlburg",
        "Neureut",
        "Nordstadt",
        "Nordweststadt",
        "Oberreut",
        "Oststadt",
        "Palmbach",
        "Rintheim",
        "Rüppurr",
        "Stupferich",
        "Südstadt",
        "Südweststadt",
        "Waldstadt",
        "Weiherfeld-Dammerstock",
        "Weststadt",
        "Wolfartsweier"
    };

    private readonly IEnumerable<IScraper> _scrapers;
    private readonly ISnapshotRepository _repository;
    private readonly SnapshotWriter _snapshotWriter;
    private readonly string _dataDirectory;
    private Func<int, Task>? _onBatchSaved;

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

    public void SetOnBatchSavedCallback(Func<int, Task> callback)
    {
        _onBatchSaved = callback;
    }

    private List<string> _scraperErrors = new();

    public IReadOnlyList<string> ScraperErrors => _scraperErrors.AsReadOnly();

    public async Task<int> ScrapeAllSourcesAsync(CancellationToken ct = default)
    {
        await _repository.InitializeAsync(ct);
        _scraperErrors.Clear();
        int totalFlats = 0;

        foreach (var scraper in _scrapers)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                Console.WriteLine($"Starting scrape for source: {scraper.Source}");
                var allListings = (await scraper.ScrapeAsync(ct, _onBatchSaved)).ToList();

                ct.ThrowIfCancellationRequested();

                if (allListings.Count == 0)
                {
                    Console.WriteLine($"No listings found for source: {scraper.Source}");
                    continue;
                }

                var listings = FilterByDistrict(allListings);
                var skippedCount = allListings.Count - listings.Count;

                if (skippedCount > 0)
                {
                    Console.WriteLine($"Skipped {skippedCount} listings without allowed districts");
                }

                if (listings.Count == 0)
                {
                    Console.WriteLine($"No listings with allowed districts for source: {scraper.Source}");
                    continue;
                }

                totalFlats += listings.Count;
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
                var errorMsg = $"{scraper.Source}: {ex.Message}";
                Console.Error.WriteLine($"Error scraping {scraper.Source}: {ex.Message}");
                _scraperErrors.Add(errorMsg);
                if (Environment.GetEnvironmentVariable("DEBUG") == "1")
                    Console.Error.WriteLine(ex.StackTrace);
            }
        }

        return totalFlats;
    }

    private List<Flat> FilterByDistrict(List<Flat> listings)
    {
        return listings
            .Where(flat => !string.IsNullOrWhiteSpace(flat.District) && AllowedDistricts.Contains(flat.District))
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await _repository.DisposeAsync();
    }
}
