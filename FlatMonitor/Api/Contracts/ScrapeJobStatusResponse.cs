namespace FlatMonitor.Api.Contracts;

using static FlatMonitor.Api.Services.ScrapeJobService;

public class ScrapeJobStatusResponse
{
    public Guid JobId { get; set; }
    public ScrapeJobStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
