namespace FlatMonitor.Api.Contracts;

/// <summary>The body of <c>POST /api/scrape</c>; optional.</summary>
public sealed class ScrapeRequest
{
    /// <summary>The sources to scrape (any case); <c>null</c> or no body: all available. The others keep their flats.</summary>
    public List<string>? Sources { get; set; }
}
