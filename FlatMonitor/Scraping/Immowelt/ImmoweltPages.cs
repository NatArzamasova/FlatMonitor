namespace FlatMonitor.Scraping.Immowelt;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// What immowelt's pages have in common: the source name, page URLs, and the data scripts
/// <c>window["NAME"]=JSON.parse("…")</c> the pages carry their data in.
/// </summary>
public static partial class ImmoweltPages
{
    public const string SourceName = "Immowelt";

    /// <summary>The list page's script; its search results are lz-string compressed (<see cref="LzString"/>).</summary>
    public const string ListDataScript = "__UFRN_FETCHER__";

    /// <summary>The expose page's script, plain JSON.</summary>
    public const string ExposeDataScript = "__UFRN_LIFECYCLE_SERVERREQUEST__";

    /// <summary>
    /// The search's results page <paramref name="page"/>: page 1 is the search URL without a <c>page</c> parameter,
    /// later pages add or replace <c>page=N</c>.
    /// </summary>
    public static string PageUrl(string searchUrl, int page)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        var withoutPage = PageParameter().Replace(searchUrl, "").TrimEnd('?', '&');
        if (page == 1)
            return withoutPage;

        return withoutPage + (withoutPage.Contains('?') ? '&' : '?') + $"page={page}";
    }

    /// <summary>
    /// The JSON text of the page's data script <paramref name="name"/>, or <c>null</c> if the page has none
    /// (the DataDome guard, a changed page).
    /// </summary>
    public static string? ReadDataScript(string html, string name)
    {
        var match = Regex.Match(html, $$"""window\["{{Regex.Escape(name)}}"\]=JSON\.parse\("((?:[^"\\]|\\.)*)"\)""");
        if (!match.Success)
            return null;

        // The argument is a string literal holding JSON; JSON.parse reads it like a JSON string
        return JsonSerializer.Deserialize<string>($"\"{match.Groups[1].Value}\"");
    }

    [GeneratedRegex(@"(?<=[?&])page=\d+&?")]
    private static partial Regex PageParameter();
}
