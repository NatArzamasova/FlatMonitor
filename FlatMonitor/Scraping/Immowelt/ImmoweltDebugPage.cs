namespace FlatMonitor.Scraping.Immowelt;

/// <summary>A page that could not be read (e.g. the DataDome guard), kept for a look at its markup.</summary>
internal static class ImmoweltDebugPage
{
    /// <summary>
    /// Saves the page to <paramref name="directory"/> (default: the temp directory) if
    /// <see cref="ImmoweltOptions.SaveDebugPages"/> is on. Returns where it went, or how to turn saving on, for the
    /// error message.
    /// </summary>
    public static async Task<string> SaveAsync(
        ImmoweltOptions options, string html, string name, CancellationToken ct, string? directory = null)
    {
        if (!options.SaveDebugPages)
            return $"set {ImmoweltOptions.SectionName}:{nameof(ImmoweltOptions.SaveDebugPages)} to true to save the page";

        var path = Path.Combine(directory ?? Path.GetTempPath(), $"immowelt_debug_{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");
        await File.WriteAllTextAsync(path, html, ct);
        return $"page ({html.Length} bytes) saved to {path}";
    }
}
