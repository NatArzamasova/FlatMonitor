using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping;

namespace FlatMonitor.Tests.Scraping.Common;

/// <summary>
/// An <see cref="IScraper"/> that yields its flats only once <paramref name="gate"/> has completed: sets the order in
/// which sources that run in parallel get on. A gate that never completes keeps it scraping until the run is canceled;
/// after 10 seconds it fails with a <see cref="TimeoutException"/> (a source that would wait forever, e.g. one that is
/// not run in parallel with the source that opens its gate).
/// </summary>
internal sealed class GatedScraper(string source, Task gate, params Flat[] flats) : IScraper
{
    public string Source => source;

    /// <summary>Called once the gate has opened, before the first flat.</summary>
    public Action<CancellationToken>? AfterGate { get; init; }

    public bool Started { get; private set; }

    public async IAsyncEnumerable<Flat> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Started = true;
        await gate.WaitAsync(TimeSpan.FromSeconds(10), ct);
        AfterGate?.Invoke(ct);

        foreach (var flat in flats)
        {
            await Task.Yield();
            yield return flat;
        }
    }
}
