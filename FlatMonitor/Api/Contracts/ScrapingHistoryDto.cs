namespace FlatMonitor.Api.Contracts;

using FlatMonitor.Models;

/// <summary>
/// A scrape run as the API serves it: the columns of <see cref="ScrapingHistory"/> the dashboard shows, so a new column
/// of the entity does not silently become API surface.
/// </summary>
public sealed record ScrapingHistoryDto(
    int Id,
    DateTime StartTimeUtc,
    DateTime? EndTimeUtc,
    ScrapingStatus Status,
    string? ErrorMessage,
    int FlatsScrapedCount)
{
    public static ScrapingHistoryDto? From(ScrapingHistory? history) => history is null
        ? null
        : new(history.Id, history.StartTimeUtc, history.EndTimeUtc, history.Status, history.ErrorMessage, history.FlatsScrapedCount);
}
