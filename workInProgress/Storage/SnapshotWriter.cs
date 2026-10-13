namespace workInProgress.Storage;

using System.Text.Json;
using workInProgress.Json;
using workInProgress.Models;

public sealed class SnapshotWriter
{
    private const string SnapshotsDir = "snapshots";

    public async Task<string> WriteSnapshotAsync(
        List<Flat> listings,
        string? baseDirectory = null,
        CancellationToken ct = default)
    {
        baseDirectory ??= StoragePaths.GetDefaultDataDirectory();

        // Ensure data/snapshots directory exists
        var snapshotsPath = Path.Combine(baseDirectory, SnapshotsDir);
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
