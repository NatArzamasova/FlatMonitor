using FlatMonitor.Models;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Storage;

/// <summary>
/// Scrape runs for tests that publish flats: FlatsArchive references the run (ScrapingHistories), so on SQLite a
/// publish needs a run row.
/// </summary>
internal static class TestRuns
{
    /// <summary>
    /// Adds a finished, successful run a day ago and returns its id. The row is not left tracked, as tests check the
    /// change tracker, and an old finished run is neither the latest nor one left running.
    /// </summary>
    public static async Task<int> AddAsync(FlatsDbContext dbContext)
    {
        var run = new ScrapingHistory
        {
            StartTimeUtc = DateTime.UtcNow.AddDays(-1),
            EndTimeUtc = DateTime.UtcNow.AddDays(-1).AddMinutes(10),
            Status = ScrapingStatus.Successful,
        };
        dbContext.ScrapingHistories.Add(run);
        await dbContext.SaveChangesAsync();
        dbContext.Entry(run).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        return run.Id;
    }
}
