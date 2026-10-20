namespace workInProgress.Scraping;

using workInProgress.Models;
using workInProgress.Storage;
using workInProgress.Scraping.Common;

public sealed class ScraperCoordinator : IAsyncDisposable
{
    private static readonly HashSet<string> AllowedDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Beiertheim-Bulach",
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
                // Set up the batch save callback: filter then save
                if (scraper is FlatScraper flatScraper)
                {
                    flatScraper.SaveListingsCallback = async (batch, batchCt) =>
                    {
                        var filtered = batch
                            .Select(flat =>
                            {
                                var normalizedDistrict = NormalizeDistrict(flat.District);
                                if (normalizedDistrict == null)
                                    return null;

                                return new Flat
                                {
                                    ListingId = flat.ListingId,
                                    Url = flat.Url,
                                    Title = flat.Title,
                                    Address = flat.Address,
                                    PostalCode = flat.PostalCode,
                                    District = normalizedDistrict,
                                    PriceEur = flat.PriceEur,
                                    OriginalPriceEur = flat.OriginalPriceEur,
                                    PricePerMeter = flat.PricePerMeter,
                                    RoomsCount = flat.RoomsCount,
                                    LivingAreaSqm = flat.LivingAreaSqm,
                                    CommissionPercent = flat.CommissionPercent,
                                    Details = flat.Details,
                                    ScrapedAtUtc = flat.ScrapedAtUtc,
                                    Source = flat.Source,
                                    DetailPageUrl = flat.DetailPageUrl,
                                };
                            })
                            .Where(f => f != null)
                            .ToList();

                        if (filtered.Count > 0)
                        {
                            await _repository.UpsertListingsAsync(filtered, batchCt);
                        }
                    };
                }

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

                // Filtering and batch-saving already happened via SaveListingsCallback during scraping
                // Write snapshot with filtered flats only
                var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(
                    listings,
                    baseDirectory: _dataDirectory,
                    source: scraper.Source,
                    ct: ct);

                Console.WriteLine($"Snapshot written for {scraper.Source}: {snapshotPath}");
                Console.WriteLine($"Batch-saved {listings.Count} filtered listings from {scraper.Source}");
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
        var result = new List<Flat>();
        foreach (var flat in listings)
        {
            var normalizedDistrict = NormalizeDistrict(flat.District);
            if (normalizedDistrict != null)
            {
                var updatedFlat = new Flat
                {
                    ListingId = flat.ListingId,
                    Url = flat.Url,
                    Title = flat.Title,
                    Address = flat.Address,
                    PostalCode = flat.PostalCode,
                    District = normalizedDistrict,
                    PriceEur = flat.PriceEur,
                    OriginalPriceEur = flat.OriginalPriceEur,
                    PricePerMeter = flat.PricePerMeter,
                    RoomsCount = flat.RoomsCount,
                    LivingAreaSqm = flat.LivingAreaSqm,
                    CommissionPercent = flat.CommissionPercent,
                    Details = flat.Details,
                    ScrapedAtUtc = flat.ScrapedAtUtc,
                    Source = flat.Source,
                    DetailPageUrl = flat.DetailPageUrl,
                };
                result.Add(updatedFlat);
            }
        }
        return result;
    }

    private string? NormalizeDistrict(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
            return null;

        // Try exact match first (case-insensitive)
        var match = AllowedDistricts.FirstOrDefault(d => d.Equals(district, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // Try substring match (case-insensitive) — handles "KA-Oberreut" containing "Oberreut"
        match = AllowedDistricts.FirstOrDefault(d => district.Contains(d, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // No match found, filter this flat out
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _repository.DisposeAsync();
    }
}
