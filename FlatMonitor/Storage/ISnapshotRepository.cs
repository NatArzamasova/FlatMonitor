namespace FlatMonitor.Storage;

using FlatMonitor.Models;

/// <summary>
/// SQLite-specific repository for persisting apartments (Flat) to the database (FlatsSnapshots table).
/// This is the CANONICAL SOURCE OF TRUTH for all flats and their data.
/// It is distinct from the JSONL snapshot files (SnapshotWriter/SnapshotLoader), which are debugging backups only.
/// The database is queried for reports and aggregations; snapshots are never replayed back into it.
/// </summary>
public interface ISnapshotRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task UpsertListingsAsync(List<Flat> listings, CancellationToken ct = default);
    Task BackupFlatsSnapshotsAsync(CancellationToken ct = default);
    Task RestoreFlatsSnapshotsAsync(CancellationToken ct = default);
    Task DeleteBackupAsync(CancellationToken ct = default);
    Task<List<FlatOffer>> GetCurrentOffersAsync(CancellationToken ct = default);
}
