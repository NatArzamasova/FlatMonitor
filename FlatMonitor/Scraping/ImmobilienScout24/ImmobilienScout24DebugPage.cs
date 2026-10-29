namespace FlatMonitor.Scraping.ImmobilienScout24;

/// <summary>A listings page that could not be read (e.g. the robot guard), kept for a look at its markup.</summary>
internal static class ImmobilienScout24DebugPage
{
    /// <summary>
    /// Saves the page to <paramref name="directory"/> (default: the temp directory) if
    /// <see cref="ImmobilienScout24Options.SaveDebugPages"/> is on. Returns where it went, or how to turn saving on,
    /// for the error message.
    /// </summary>
    public static async Task<string> SaveAsync(ImmobilienScout24Options options, string html, CancellationToken ct, string? directory = null)
    {
        if (!options.SaveDebugPages)
            return $"set {ImmobilienScout24Options.SectionName}:{nameof(ImmobilienScout24Options.SaveDebugPages)} to true to save the page";

        var path = Path.Combine(directory ?? Path.GetTempPath(), $"immobilienscout24_debug_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");
        await File.WriteAllTextAsync(path, html, ct);
        return $"page ({html.Length} bytes) saved to {path}";
    }
}
