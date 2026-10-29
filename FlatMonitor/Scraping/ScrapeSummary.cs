namespace FlatMonitor.Scraping;

/// <summary>Result of one <see cref="ScraperCoordinator.ScrapeAllSourcesAsync"/> run.</summary>
/// <param name="FlatsPerSource">Flats saved per source (after district filtering), in scrape order. A failed source keeps the flats it saved before failing.</param>
/// <param name="Errors">One "Source: message" entry per failed source.</param>
public sealed record ScrapeSummary(IReadOnlyDictionary<string, int> FlatsPerSource, IReadOnlyList<string> Errors)
{
    public int TotalFlats => FlatsPerSource.Values.Sum();

    public bool Success => Errors.Count == 0;
}
