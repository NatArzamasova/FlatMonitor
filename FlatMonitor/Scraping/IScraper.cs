namespace FlatMonitor.Scraping;

using FlatMonitor.Models;


public interface IScraper
{
    string Source { get; }
    Task<IEnumerable<Flat>> ScrapeAsync(CancellationToken ct = default);
}
