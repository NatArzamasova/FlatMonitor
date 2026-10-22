namespace FlatMonitor.Api.Contracts;

public class ScrapeQueuedResponse
{
    public Guid JobId { get; set; }
    public string Message { get; set; } = "";
    public DateTime QueuedAtUtc { get; set; }
}
