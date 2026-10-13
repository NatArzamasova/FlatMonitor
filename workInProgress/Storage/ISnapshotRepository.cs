namespace workInProgress.Storage;

using workInProgress.Models;

public interface ISnapshotRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task UpsertListingsAsync(List<Listing> listings, CancellationToken ct = default);
}
