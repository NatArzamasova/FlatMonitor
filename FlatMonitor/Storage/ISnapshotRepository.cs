namespace workInProgress.Storage;

using workInProgress.Models;

/// <summary>
/// SQLite-specific repository for persisting apartments (Flat) to the database.
/// This is the derived data layer that upserts deduplicated apartments to SQLite.
/// It is distinct from the JSONL snapshot file layer (SnapshotWriter/SnapshotLoader).
/// Will be made source-aware in Phase 2 alongside SQLite schema migration.
/// </summary>
public interface ISnapshotRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task UpsertListingsAsync(List<Flat> listings, CancellationToken ct = default);
    Task BackupFlatsSnapshotsAsync(CancellationToken ct = default);
    Task RestoreFlatsSnapshotsAsync(CancellationToken ct = default);
    Task DeleteBackupAsync(CancellationToken ct = default);
}
