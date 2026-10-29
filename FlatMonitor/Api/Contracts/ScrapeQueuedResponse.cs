namespace FlatMonitor.Api.Contracts;

/// <summary>Data of <c>POST /api/scrape</c>; the message is in the surrounding <see cref="ApiResponse{T}"/>.</summary>
public class ScrapeQueuedResponse
{
    public Guid JobId { get; set; }
    public DateTime QueuedAtUtc { get; set; }
}
