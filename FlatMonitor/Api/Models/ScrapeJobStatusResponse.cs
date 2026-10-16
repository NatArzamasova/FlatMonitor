namespace workInProgress.Api.Models;

public class ScrapeJobStatusResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
