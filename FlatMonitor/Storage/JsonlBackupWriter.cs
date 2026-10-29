namespace FlatMonitor.Storage;

using System.Text.Json;
using FlatMonitor.Json;
using FlatMonitor.Models;



/// <summary>
/// Writes scraped flats to JSONL backup files (under <c>data/snapshots</c>) for debugging purposes only.
/// The backups are NOT used to rebuild the database; they exist for troubleshooting.
/// The SQLite database (FlatsSnapshots table) is the canonical source of truth.
/// </summary>
public sealed class JsonlBackupWriter
{
    private const string SnapshotsDir = "snapshots";

    public async Task<string> WriteSnapshotAsync(
        List<Flat> listings,
        string baseDirectory,
        string? source = null,
        CancellationToken ct = default)
    {
        await using var file = Create(baseDirectory, source);
        await file.AppendAsync(listings, ct);
        return file.Path;
    }

    /// <summary>Creates a new, empty backup file; append flats to it batch by batch while scraping.</summary>
    public JsonlBackupFile Create(string baseDirectory, string? source = null)
    {
        source ??= "default";

        // Ensure data/snapshots/{source} directory exists
        var snapshotsPath = Path.Combine(baseDirectory, SnapshotsDir, source);
        Directory.CreateDirectory(snapshotsPath);

        // Generate timestamped filename: {utc-timestamp}.jsonl
        string timestamp = DateTime.UtcNow.ToString("O").Replace(":", "-").Split('.')[0];
        string filename = $"{timestamp}.jsonl";
        return new JsonlBackupFile(Path.Combine(snapshotsPath, filename));
    }
}

/// <summary>An open JSONL backup file (one JSON object per line).</summary>
public sealed class JsonlBackupFile : IAsyncDisposable
{
    private readonly StreamWriter _writer;

    internal JsonlBackupFile(string path)
    {
        Path = path;
        _writer = new StreamWriter(path, append: false);
    }

    public string Path { get; }

    public async Task AppendAsync(IEnumerable<Flat> listings, CancellationToken ct = default)
    {
        foreach (var listing in listings)
        {
            var json = JsonSerializer.Serialize(listing, JsonOptions.JsonlBackup);
            await _writer.WriteLineAsync(json.AsMemory(), ct);
        }
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();
}
