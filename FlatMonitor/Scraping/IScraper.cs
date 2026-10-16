namespace workInProgress.Scraping;

using workInProgress.Models;

public interface IScraper
{
    string Source { get; }
    Task<IEnumerable<Flat>> ScrapeAsync(CancellationToken ct = default, Func<int, Task>? onBatchSaved = null);
}
