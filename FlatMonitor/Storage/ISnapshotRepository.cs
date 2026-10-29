namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using FlatMonitor.Storage.ReadModels;

/// <summary>
/// SQLite-specific repository for persisting apartments (Flat) to the database (FlatsSnapshots table).
/// This is the CANONICAL SOURCE OF TRUTH for all flats and their data.
/// It is distinct from the JSONL snapshot files (SnapshotWriter), which are debugging backups only.
/// The database is queried for reports and aggregations; snapshots are never replayed back into it.
/// </summary>
/// <remarks>
/// A scrape never touches FlatsSnapshots while it runs: it writes into a staging table
/// (<see cref="BeginRunAsync"/>, <see cref="AddToRunAsync"/>) and replaces one source at a time once that source
/// has finished (<see cref="PublishSourceAsync"/>). A source that fails is not published and keeps its rows.
/// </remarks>
public interface ISnapshotRepository
{
    /// <summary>Starts a scrape run with an empty staging table (dropping what an interrupted run left there).</summary>
    Task BeginRunAsync(CancellationToken ct = default);

    /// <summary>Adds flats to the run. A flat added twice keeps the values it was added with last.</summary>
    Task AddToRunAsync(List<Flat> flats, CancellationToken ct = default);

    /// <summary>
    /// Replaces the source's rows in FlatsSnapshots with the run's rows of that source, in one transaction.
    /// A flat that was already there keeps its CreatedAtUtc. Returns the number of rows the source now has.
    /// </summary>
    Task<int> PublishSourceAsync(string source, CancellationToken ct = default);

    /// <summary>Ends a run: empties the staging table. FlatsSnapshots keeps whatever was published.</summary>
    Task DiscardRunAsync(CancellationToken ct = default);

    Task<List<FlatOffer>> GetCurrentOffersAsync(CancellationToken ct = default);
}
