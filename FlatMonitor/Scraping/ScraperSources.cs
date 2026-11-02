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
