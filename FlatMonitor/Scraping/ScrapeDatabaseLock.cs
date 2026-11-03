namespace FlatMonitor.Scraping;

/// <summary>
/// Lets one database call of a scrape run at a time. The sources are scraped in parallel, but all of them write through
/// the scrape's one <c>FlatsDbContext</c>, which is not thread-safe, into SQLite, which has one writer at a time.
/// Database calls take milliseconds and the scraping minutes, so the sources hardly ever wait for each other.
/// Only database work belongs inside: a download or a page load would hold up every other source.
/// </summary>
public sealed class ScrapeDatabaseLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> databaseWork, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await databaseWork();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task RunAsync(Func<Task> databaseWork, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            await databaseWork();
            return true;
        }, ct);
}
