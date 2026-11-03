namespace FlatMonitor.Scraping;

using System.Collections.Concurrent;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Stores the title photos of scraped flats as previews (FlatPreviews).</summary>
public interface IFlatPreviewDownloader
{
    /// <summary>
    /// Downloads and stores the photo (<see cref="Flat.ImageUrl"/>) of each flat whose stored preview is missing or has
    /// another URL. Never throws but on cancellation through <paramref name="ct"/>: a flat without a preview is still a
    /// flat. Returns the number of previews stored.
    /// </summary>
    Task<int> SavePreviewsAsync(IReadOnlyCollection<Flat> flats, CancellationToken ct = default);
}

/// <summary>
/// Downloads the photos with a plain <see cref="HttpClient"/>: they come from the portals' picture servers
/// (media.ohne-makler.net, pictures.immobilienscout24.de, mms.immowelt.de), which are not behind the pages' robot
/// guards. The parsers already ask those servers for a preview size, so a photo is 10–30 KB.
/// </summary>
public sealed class FlatPreviewDownloader : IFlatPreviewDownloader
{
    /// <summary>A few at a time: a batch of 20 takes about a second instead of four.</summary>
    private const int MaxParallelDownloads = 4;

    private readonly HttpClient _httpClient;
    private readonly IFlatPreviewRepository _repository;
    private readonly ILogger<FlatPreviewDownloader> _logger;

    public FlatPreviewDownloader(HttpClient httpClient, IFlatPreviewRepository repository, ILogger<FlatPreviewDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> SavePreviewsAsync(IReadOnlyCollection<Flat> flats, CancellationToken ct = default)
    {
        try
        {
            var toDownload = await WithoutCurrentPreviewAsync(flats, ct);
            if (toDownload.Count == 0)
                return 0;

            var previews = new ConcurrentBag<FlatPreview>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = ct };
            await Parallel.ForEachAsync(toDownload, options, async (flat, token) =>
            {
                if (await DownloadAsync(flat, token) is { } preview)
                    previews.Add(preview);
            });

            // Database access stays sequential: the context is shared with the whole scrape
            await _repository.SaveAsync(previews.ToList(), ct);

            var failed = toDownload.Count - previews.Count;
            _logger.Log(failed > 0 ? LogLevel.Warning : LogLevel.Debug,
                "Previews: {Saved} of {Count} downloaded and saved, {Failed} failed", previews.Count, toDownload.Count, failed);
            return previews.Count;
        }
        // Only the caller's token cancels; an HttpClient timeout is a TaskCanceledException too
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not save the previews of {Count} flats", flats.Count);
            return 0;
        }
    }

    /// <summary>The flats with a photo whose stored preview is missing or comes from another URL, once per flat.</summary>
    private async Task<List<Flat>> WithoutCurrentPreviewAsync(IReadOnlyCollection<Flat> flats, CancellationToken ct)
    {
        var result = new List<Flat>();
        var withImage = flats
            .Where(flat => !string.IsNullOrEmpty(flat.ImageUrl))
            .DistinctBy(flat => (flat.Source, flat.ListingId));
        foreach (var bySource in withImage.GroupBy(flat => flat.Source))
        {
            var stored = await _repository.GetImageUrlsAsync(bySource.Key, bySource.Select(flat => flat.ListingId).ToList(), ct);
            result.AddRange(bySource.Where(flat => !stored.TryGetValue(flat.ListingId, out var url) || url != flat.ImageUrl));
        }

        return result;
    }

    /// <summary>The flat's photo, or <c>null</c> (logged) if it cannot be downloaded or is no image of preview size.</summary>
    private async Task<FlatPreview?> DownloadAsync(Flat flat, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(flat.ImageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return Skip(flat, $"HTTP {(int)response.StatusCode}");

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (contentType == null || !FlatPreview.ContentTypes.Contains(contentType))
                return Skip(flat, $"no photo ({contentType ?? "no content type"})");

            if (response.Content.Headers.ContentLength > FlatPreview.MaxBytes)
                return Skip(flat, $"too large ({response.Content.Headers.ContentLength} bytes)");

            var data = await ReadAtMostAsync(response.Content, FlatPreview.MaxBytes, ct);
            if (data == null)
                return Skip(flat, "too large");
            if (data.Length == 0)
                return Skip(flat, "empty");

            return new FlatPreview
            {
                Source = flat.Source,
                ListingId = flat.ListingId,
                ImageUrl = flat.ImageUrl!,
                ContentType = contentType,
                Data = data,
                DownloadedAtUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Skip(flat, ex.Message);
        }
    }

    private FlatPreview? Skip(Flat flat, string reason)
    {
        _logger.LogDebug("{Source}: listing {ListingId}: no preview from {ImageUrl}: {Reason}",
            flat.Source, flat.ListingId, flat.ImageUrl, reason);
        return null;
    }

    /// <summary>The body, or <c>null</c> if it is longer than <paramref name="maxBytes"/> (a missing Content-Length).</summary>
    private static async Task<byte[]?> ReadAtMostAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
