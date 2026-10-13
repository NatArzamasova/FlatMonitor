namespace workInProgress.Storage;

using System.Text.Json;
using workInProgress.Json;
using workInProgress.Models;

public sealed class SnapshotLoader
{
    private const string SnapshotsDir = "snapshots";

    public List<Listing> LoadAllSnapshots(string? baseDirectory = null)
    {
        baseDirectory ??= StoragePaths.GetDefaultDataDirectory();
        var snapshotsPath = Path.Combine(baseDirectory, SnapshotsDir);
        var listings = new List<Listing>();

        if (!Directory.Exists(snapshotsPath))
            return listings;

        var snapshotFiles = Directory.GetFiles(snapshotsPath, "*.jsonl")
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in snapshotFiles)
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                Listing? listing;
                try
                {
                    listing = JsonSerializer.Deserialize<Listing>(line, JsonOptions.Snapshot);
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
