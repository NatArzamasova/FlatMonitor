namespace FlatMonitor.Scraping.ImmobilienScout24;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Settings of the ImmobilienScout24 scraper, from the <c>Scrapers:ImmobilienScout24</c> section of
/// <c>appsettings.json</c> (overridable by environment variables such as <c>Scrapers__ImmobilienScout24__Headless</c>).
/// A missing value keeps its default.
/// </summary>
public sealed record ImmobilienScout24Options
{
    public const string SectionName = "Scrapers:ImmobilienScout24";
    public const string DefaultSearchUrl = "https://www.immobilienscout24.de/Suche/de/baden-wuerttemberg/karlsruhe/wohnung-kaufen";

    /// <summary>Runs the browser without a window. Off by default: the robot guard may need a person to solve it.</summary>
    public bool Headless { get; init; }

    /// <summary>The first listings page; the scraper follows its pagination from there.</summary>
    public string SearchUrl { get; init; } = DefaultSearchUrl;

    /// <summary>Saves a suspiciously small first listings page (for example a robot guard page) to the temp directory.</summary>
    public bool SaveDebugPages { get; init; }

    public static ImmobilienScout24Options FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var defaults = new ImmobilienScout24Options();

        return new ImmobilienScout24Options
        {
            Headless = ReadBool(section, nameof(Headless)) ?? defaults.Headless,
            SearchUrl = string.IsNullOrWhiteSpace(section[nameof(SearchUrl)]) ? defaults.SearchUrl : section[nameof(SearchUrl)]!,
            SaveDebugPages = ReadBool(section, nameof(SaveDebugPages)) ?? defaults.SaveDebugPages,
        };
    }

    private static bool? ReadBool(IConfiguration section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return bool.TryParse(value, out var result)
            ? result
            : throw new InvalidOperationException($"Configuration '{SectionName}:{key}' must be true or false, but is '{value}'.");
    }
}
