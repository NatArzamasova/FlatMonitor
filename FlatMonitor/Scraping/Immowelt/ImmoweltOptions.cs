namespace FlatMonitor.Scraping.Immowelt;

using System.Globalization;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Settings of the immowelt scraper, from the <c>Scrapers:Immowelt</c> section of <c>appsettings.json</c>
/// (overridable by environment variables such as <c>Scrapers__Immowelt__CdpUrl</c>). A missing value keeps its default.
/// </summary>
public sealed record ImmoweltOptions
{
    public const string SectionName = "Scrapers:Immowelt";

    /// <summary>
    /// Where a Chrome started with <c>--remote-debugging-port=9222</c> listens. Not <c>localhost</c>: that resolves to
    /// IPv6 <c>::1</c>, but Chrome listens on IPv4 only.
    /// </summary>
    public const string DefaultCdpUrl = "http://127.0.0.1:9222";

    /// <summary>Flats to buy in Karlsruhe; auctions (<c>Buy_Auction</c>, <c>Compulsory_Auction</c>) left out.</summary>
    public const string DefaultSearchUrl =
        "https://www.immowelt.de/classified-search?distributionTypes=Buy&estateTypes=Apartment&locations=AD08DE5585";

    /// <summary>Opening pages back to back looks like a robot to DataDome ("übermenschliche Geschwindigkeit").</summary>
    public const int DefaultPauseBetweenPagesMs = 5_000;

    /// <summary>Time for the user to pass DataDome's robot check in the scraper's tab.</summary>
    public const int DefaultRobotCheckWaitSeconds = 600;

    /// <summary>Chrome's own profile for the scraper: recent Chrome refuses remote debugging on the default profile.</summary>
    public const string DefaultChromeUserDataDir = @"%LOCALAPPDATA%\FlatMonitorChrome";

    /// <summary>
    /// The user's Chrome the scraper attaches to: DataDome blocks Playwright's own browser, even headed, so the scraper
    /// cannot use one.
    /// </summary>
    public string CdpUrl { get; init; } = DefaultCdpUrl;

    /// <summary>
    /// If no Chrome listens at <see cref="CdpUrl"/>, start one (the installed Chrome, as the user would) and close it
    /// after the run. Off: the user starts Chrome.
    /// </summary>
    public bool LaunchChrome { get; init; } = true;

    /// <summary>The <c>chrome.exe</c> to start; <c>null</c>: found in the usual install locations.</summary>
    public string? ChromePath { get; init; }

    /// <summary>The profile of the started Chrome; environment variables such as <c>%LOCALAPPDATA%</c> are expanded.</summary>
    public string ChromeUserDataDir { get; init; } = DefaultChromeUserDataDir;

    /// <summary>The first results page; the scraper opens the further pages by URL (<see cref="ImmoweltPages.PageUrl"/>).</summary>
    public string SearchUrl { get; init; } = DefaultSearchUrl;

    /// <summary>The pause between two pages, list pages and exposes alike.</summary>
    public int PauseBetweenPagesMs { get; init; } = DefaultPauseBetweenPagesMs;

    /// <summary>
    /// When DataDome shows its robot check, how long the scraper keeps the tab open and waits for the user to pass it
    /// (<see cref="ImmoweltRobotCheck"/>). 0: do not wait, fail (results page) or keep the card data (expose) at once.
    /// </summary>
    public int RobotCheckWaitSeconds { get; init; } = DefaultRobotCheckWaitSeconds;

    /// <summary>Saves a page without data (for example the DataDome guard) to the temp directory.</summary>
    public bool SaveDebugPages { get; init; }

    public static ImmoweltOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var defaults = new ImmoweltOptions();

        return new ImmoweltOptions
        {
            CdpUrl = ReadString(section, nameof(CdpUrl)) ?? defaults.CdpUrl,
            LaunchChrome = ReadBool(section, nameof(LaunchChrome)) ?? defaults.LaunchChrome,
            ChromePath = ReadString(section, nameof(ChromePath)) ?? defaults.ChromePath,
            ChromeUserDataDir = ReadString(section, nameof(ChromeUserDataDir)) ?? defaults.ChromeUserDataDir,
            SearchUrl = ReadString(section, nameof(SearchUrl)) ?? defaults.SearchUrl,
            PauseBetweenPagesMs = ReadNonNegativeInt(section, nameof(PauseBetweenPagesMs)) ?? defaults.PauseBetweenPagesMs,
            RobotCheckWaitSeconds = ReadNonNegativeInt(section, nameof(RobotCheckWaitSeconds)) ?? defaults.RobotCheckWaitSeconds,
            SaveDebugPages = ReadBool(section, nameof(SaveDebugPages)) ?? defaults.SaveDebugPages,
        };
    }

    private static string? ReadString(IConfiguration section, string key) =>
        string.IsNullOrWhiteSpace(section[key]) ? null : section[key]!.Trim();

    private static int? ReadNonNegativeInt(IConfiguration section, string key)
    {
        var value = ReadString(section, key);
        if (value == null)
            return null;

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException(
                $"Configuration '{SectionName}:{key}' must be a whole number of 0 or more, but is '{value}'.");
    }

    private static bool? ReadBool(IConfiguration section, string key)
    {
        var value = ReadString(section, key);
        if (value == null)
            return null;

        return bool.TryParse(value, out var result)
            ? result
            : throw new InvalidOperationException($"Configuration '{SectionName}:{key}' must be true or false, but is '{value}'.");
    }
}
