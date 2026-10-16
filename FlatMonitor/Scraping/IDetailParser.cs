namespace workInProgress.Scraping;

using workInProgress.Models;

/// <summary>
/// Interface for parsing detail fields from source-specific detail pages.
/// Implemented by each source parser (OhneMakler, ImmobilienScout24, etc.).
/// </summary>
public interface IDetailParser
{
    /// <summary>
    /// Parse detail fields from a detail page HTML string.
    /// </summary>
    /// <param name="html">The HTML content of a detail page</param>
    /// <returns>A Details object if successfully parsed; null if parsing fails or structure is invalid</returns>
    FlatDetails? ParseDetailFields(string html);
}
