namespace workInProgress.Storage;

public interface IFlatAggregationRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
}
