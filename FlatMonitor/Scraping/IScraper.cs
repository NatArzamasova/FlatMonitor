namespace FlatMonitor.Scraping;

using FlatMonitor.Models;


public interface IScraper
{
    string Source { get; }

    /// <summary>
    /// Yields the source's flats one by one, unfiltered. Scrapers know nothing about filtering, batching or
    /// persistence; <see cref="ScraperCoordinator"/> does that. Failures surface as exceptions while enumerating.
    /// </summary>
    IAsyncEnumerable<Flat> ScrapeAsync(CancellationToken ct = default);
}
