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
                SetupBatchSaveCallback(scraper);
                var allListings = await ScrapeSourceAsync(scraper, ct);

                if (allListings.Count == 0)
                    continue;

                totalFlats += allListings.Count;
                await WriteSnapshotAsync(scraper, allListings, ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Scraping canceled for {scraper.Source}");
                throw;
            }
            catch (Exception ex)
            {
                HandleScraperError(scraper, ex);
            }
        }

        return totalFlats;
    }

    private void SetupBatchSaveCallback(IScraper scraper)
    {
        if (scraper is not FlatScraper flatScraper)
            return;

        flatScraper.SaveListingsCallback = async (batch, batchCt) =>
        {
            var filtered = FilterAndNormalizeBatch(batch);
            if (filtered.Count > 0)
                await _repository.UpsertListingsAsync(filtered, batchCt);
        };
    }

    private List<Flat> FilterAndNormalizeBatch(List<Flat> batch)
    {
        return batch
            .Select(flat =>
            {
                var normalizedDistrict = NormalizeDistrict(flat.District);
                return normalizedDistrict != null ? CreateFlatWithAdjustedDistrict(flat, normalizedDistrict) : null;
            })
            .Where(f => f != null)
            .ToList();
    }

    private async Task<List<Flat>> ScrapeSourceAsync(IScraper scraper, CancellationToken ct)
    {
        Console.WriteLine($"Starting scrape for source: {scraper.Source}");
        var listings = (await scraper.ScrapeAsync(ct, _onBatchSaved)).ToList();

        if (listings.Count == 0)
        {
            Console.WriteLine($"No listings found for source: {scraper.Source}");
            return listings;
        }

        // Fallback filter for scrapers that don't use batch saving callback (e.g., test mocks)
        // Production scrapers (FlatScraper) already filtered via callback during scraping
        var filtered = FilterAndNormalizeBatch(listings);
        var skipped = listings.Count - filtered.Count;
        if (skipped > 0)
            Console.WriteLine($"Skipped {skipped} listings without allowed districts");

        if (filtered.Count > 0)
            await _repository.UpsertListingsAsync(filtered);

        return filtered;
    }

    private async Task WriteSnapshotAsync(IScraper scraper, List<Flat> listings, CancellationToken ct)
    {
        var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(
            listings,
            baseDirectory: _dataDirectory,
            source: scraper.Source,
            ct: ct);

        Console.WriteLine($"Snapshot written for {scraper.Source}: {snapshotPath}");
    }

    private void HandleScraperError(IScraper scraper, Exception ex)
    {
        var errorMsg = $"{scraper.Source}: {ex.Message}";
        Console.Error.WriteLine($"Error scraping {scraper.Source}: {ex.Message}");
        _scraperErrors.Add(errorMsg);
        if (Environment.GetEnvironmentVariable("DEBUG") == "1")
            Console.Error.WriteLine(ex.StackTrace);
    }

    private static Flat CreateFlatWithAdjustedDistrict(Flat flat, string normalizedDistrict)
    {
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
