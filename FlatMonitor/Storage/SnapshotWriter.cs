namespace workInProgress.Storage;

using System.Text.Json;
using workInProgress.Json;
using workInProgress.Models;

/// <summary>
/// Writes scraped flats to JSONL snapshot files for debugging purposes only.
/// Snapshots are NOT used to rebuild the database; they exist as optional backups for troubleshooting.
/// The SQLite database (FlatsSnapshots table) is the canonical source of truth.
/// </summary>
public sealed class SnapshotWriter
{
    private const string SnapshotsDir = "snapshots";

    public async Task<string> WriteSnapshotAsync(
        List<Flat> listings,
        string? baseDirectory = null,
        string? source = null,
        CancellationToken ct = default)
    {
        baseDirectory ??= StoragePaths.GetDefaultDataDirectory();
        source ??= "default";

        // Ensure data/snapshots/{source} directory exists
        var snapshotsPath = Path.Combine(baseDirectory, SnapshotsDir, source);
        Directory.CreateDirectory(snapshotsPath);

        // Generate timestamped filename: {utc-timestamp}.jsonl
        string timestamp = DateTime.UtcNow.ToString("O").Replace(":", "-").Split('.')[0];
        string filename = $"{timestamp}.jsonl";
        string filePath = Path.Combine(snapshotsPath, filename);

        // Write listings as JSONL (one JSON object per line)
        using var writer = new StreamWriter(filePath, append: false);
        foreach (var listing in listings)
        {
            var json = JsonSerializer.Serialize(listing, JsonOptions.Snapshot);
            await writer.WriteLineAsync(json);
        }

        return filePath;
    }
}
