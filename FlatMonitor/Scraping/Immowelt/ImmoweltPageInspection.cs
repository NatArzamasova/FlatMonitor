namespace FlatMonitor.Scraping.Immowelt;

using System.Text.RegularExpressions;

/// <summary>
/// What an explored immowelt page contains, for deciding how to parse it: is it the DataDome guard, how many expose
/// links does it have, and which embedded data blobs (JSON in scripts) does it carry.
/// </summary>
public sealed record ImmoweltPageInspection(
    int Bytes,
    bool IsDataDomeGuard,
    IReadOnlyList<string> ExposeUrls,
    IReadOnlyList<string> DataMarkers)
{
    public const string BaseUrl = "https://www.immowelt.de";

    /// <summary>Script markers of server-rendered data that could be parsed instead of the markup.</summary>
    public static readonly IReadOnlyList<string> KnownDataMarkers =
    [
        "__UFRN_LIFECYCLE_SERVERREQUEST__",
        "__NEXT_DATA__",
        "__INITIAL_STATE__",
        "__NUXT__",
        "application/ld+json",
    ];

    private static readonly Regex ExposeHref = new("""href="(?<url>(?:https://www\.immowelt\.de)?/expose/[^"?#]+)""", RegexOptions.Compiled);

    public static ImmoweltPageInspection Inspect(string html)
    {
        var exposeUrls = ExposeHref.Matches(html)
            .Select(m => m.Groups["url"].Value)
            .Select(url => url.StartsWith('/') ? BaseUrl + url : url)
            .Distinct()
            .ToList();

        return new ImmoweltPageInspection(
            html.Length,
            html.Contains("captcha-delivery.com", StringComparison.Ordinal),
            exposeUrls,
            KnownDataMarkers.Where(marker => html.Contains(marker, StringComparison.Ordinal)).ToList());
    }

    public override string ToString() =>
        $"{Bytes} bytes, DataDome guard: {(IsDataDomeGuard ? "yes" : "no")}, expose links: {ExposeUrls.Count}, " +
        $"data blobs: {(DataMarkers.Count == 0 ? "none" : string.Join(", ", DataMarkers))}";
}
