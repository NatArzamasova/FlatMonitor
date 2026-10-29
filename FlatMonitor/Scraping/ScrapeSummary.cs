namespace FlatMonitor.Scraping;

/// <summary>Result of one <see cref="ScraperCoordinator.ScrapeAllSourcesAsync"/> run.</summary>
/// <param name="FlatsPerSource">
/// Flats scraped per source (after district filtering), in scrape order. A failed source counts the flats it scraped
/// before failing, but they are not published: FlatsSnapshots keeps that source's rows of the previous run.
/// </param>
/// <param name="Errors">One "Source: message" entry per failed source.</param>
public sealed record ScrapeSummary(IReadOnlyDictionary<string, int> FlatsPerSource, IReadOnlyList<string> Errors)
{
    public int TotalFlats => FlatsPerSource.Values.Sum();

    public bool Success => Errors.Count == 0;
}
