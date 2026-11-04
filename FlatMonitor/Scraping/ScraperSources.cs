namespace FlatMonitor.Scraping;

using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping.Immowelt;
using FlatMonitor.Scraping.OhneMakler;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Which sources a scrape runs: <c>Scrapers:Sources</c>, a comma-separated list of source names (any case), e.g.
/// <c>Immowelt</c> or <c>OhneMakler,Immowelt</c>; missing or empty runs all. The CLI sets it with
/// <c>scrape --source</c>. A source left out keeps its flats: the run replaces only the rows of the sources it scraped.
/// </summary>
public static class ScraperSources
{
    public const string ConfigurationKey = "Scrapers:Sources";

    /// <summary>All sources, in the order they are scraped.</summary>
    public static readonly IReadOnlyList<string> All =
        [OhneMaklerScraper.SourceName, ImmobilienScout24Scraper.SourceName, ImmoweltPages.SourceName];

    /// <summary>The selected sources, named as in <see cref="All"/>. Throws for a name that is no source.</summary>
    public static IReadOnlySet<string> FromConfiguration(IConfiguration configuration) => Parse(configuration[ConfigurationKey]);

    /// <summary>The sources of a <see cref="ConfigurationKey"/> value; see <see cref="FromConfiguration"/>.</summary>
    public static IReadOnlySet<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return All.ToHashSet();

        var selected = new HashSet<string>();
        foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            selected.Add(All.FirstOrDefault(source => source.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Configuration '{ConfigurationKey}': '{name}' is no source; the sources are {string.Join(", ", All)}."));
        }

        return selected;
    }
}

/// <summary>
/// The sources this process can scrape (those of <see cref="ScraperSources.ConfigurationKey"/>), in the order of
/// <see cref="ScraperSources.All"/>. A scrape started from the API runs all of them or the ones it names.
/// </summary>
public sealed record AvailableScraperSources(IReadOnlyList<string> Names)
{
    /// <summary>
    /// The sources a run scrapes: all available for <c>null</c>, otherwise the <paramref name="requested"/> ones (any
    /// case), in the order of <see cref="Names"/>. Throws <see cref="ArgumentException"/> for none, or for one that is
    /// not available.
    /// </summary>
    public IReadOnlyList<string> Select(IEnumerable<string>? requested)
    {
        if (requested == null)
            return Names;

        var selected = new HashSet<string>();
        foreach (var name in requested)
        {
            selected.Add(Names.FirstOrDefault(source => source.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"'{name}' is no source to scrape; the sources are {string.Join(", ", Names)}."));
        }

        if (selected.Count == 0)
            throw new ArgumentException("Select at least one source to scrape.");

        return Names.Where(selected.Contains).ToList();
    }
}
