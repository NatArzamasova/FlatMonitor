namespace workInProgress.Storage;

using workInProgress.Api.Models;
using workInProgress.Models;

public interface IFlatAggregationRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    Task<List<FlatHistoryDto>> GetHistoryAsync(CancellationToken ct = default);
    Task<List<FlatAggregation>> GetLatestAsync(CancellationToken ct = default);
}
