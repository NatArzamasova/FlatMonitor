namespace FlatMonitor.Scraping.Debug;

using System.Text.Json;
using FlatMonitor.Json;
using FlatMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;



/// <summary>
/// Loads flats from JSONL snapshot files for debugging/analysis purposes.
/// NOT used in normal application flow. The SQLite database (FlatsSnapshots table) is the canonical source.
/// Snapshots are optional backups only and may contain stale data.
/// </summary>
public sealed class SnapshotLoader
{
    private const string SnapshotsDir = "snapshots";

    private readonly ILogger _logger;

    public SnapshotLoader(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public List<Flat> LoadAllSnapshots(string baseDirectory)
    {
        var snapshotsPath = Path.Combine(baseDirectory, SnapshotsDir);
        var listings = new List<Flat>();

        if (!Directory.Exists(snapshotsPath))
            return listings;

        var snapshotFiles = Directory.GetFiles(snapshotsPath, "*.jsonl", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in snapshotFiles)
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                Flat? listing;
                try
                {
                    listing = JsonSerializer.Deserialize<Flat>(line, JsonOptions.Snapshot);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed snapshot line in {File}", Path.GetFileName(file));
                    continue;
                }

                if (listing != null)
                    listings.Add(listing);
            }
        }

        return listings;
    }
}
