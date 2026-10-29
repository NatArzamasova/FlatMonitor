namespace FlatMonitor.Storage;

using Microsoft.Extensions.Logging;

/// <summary>
/// Debugging copies of the detail pages one source fetched during the current scrape:
/// <c>{DataDirectory}/snapshots/html/{source}/{listingId}_{yyyyMMdd_HHmmss}.html</c>.
/// Cleared at the start of every scrape. Failures are logged, never thrown: the snapshots must not stop a scrape.
/// </summary>
public sealed class HtmlSnapshotStore
{
    private readonly ILogger _logger;

    public HtmlSnapshotStore(string directory, ILogger logger)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Directory { get; }

    /// <summary>The store for <paramref name="source"/> under the configured data directory.</summary>
    public static HtmlSnapshotStore ForSource(StorageOptions storage, string source, ILogger logger) =>
        new(Path.Combine(storage.DataDirectory, "snapshots", "html", source), logger);

    /// <summary>Deletes the previous scrape's snapshots.</summary>
    public Task ClearAsync(CancellationToken ct = default)
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory))
                return Task.CompletedTask;

            var files = System.IO.Directory.GetFiles(Directory, "*.html");
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                File.Delete(file);
            }
            _logger.LogDebug("Cleared {Count} previous HTML snapshots from {Directory}", files.Length, Directory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not clear previous HTML snapshots in {Directory}", Directory);
        }

        return Task.CompletedTask;
    }

    /// <summary>Saves one detail page; the returned task completes when the file is written.</summary>
    public async Task SaveAsync(string listingId, string html, CancellationToken ct = default)
    {
        var fileName = $"{ToFileNamePart(listingId)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
        var path = Path.Combine(Directory, fileName);
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            await File.WriteAllTextAsync(path, html, ct);
            _logger.LogDebug("Detail page saved to {Path}", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not save HTML snapshot {Path}", path);
        }
    }

    private static string ToFileNamePart(string listingId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(listingId.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
