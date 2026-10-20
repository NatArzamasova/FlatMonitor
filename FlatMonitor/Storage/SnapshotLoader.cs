namespace workInProgress.Storage;

using System.Text.Json;
using workInProgress.Json;
using workInProgress.Models;

/// <summary>
/// Loads flats from JSONL snapshot files for debugging/analysis purposes.
/// NOT used in normal application flow. The SQLite database (FlatsSnapshots table) is the canonical source.
/// Snapshots are optional backups only and may contain stale data.
/// </summary>
public sealed class SnapshotLoader
{
    private const string SnapshotsDir = "snapshots";

    public List<Flat> LoadAllSnapshots(string? baseDirectory = null)
    {
        baseDirectory ??= StoragePaths.GetDefaultDataDirectory();
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
                    Console.WriteLine($"Warning: Skipping malformed snapshot line in {Path.GetFileName(file)}: {ex.Message}");
                    continue;
                }

                if (listing != null)
                    listings.Add(listing);
            }
        }

        return listings;
    }
}
