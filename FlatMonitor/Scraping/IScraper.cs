namespace workInProgress.Scraping;

using workInProgress.Models;

public interface IScraper
{
    string Source { get; }
    Task<IEnumerable<Flat>> ScrapeAsync();
}
