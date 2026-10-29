namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using FlatMonitor.Storage.ReadModels;

public interface IFlatAggregationRepository
{
    Task RefreshAsync(CancellationToken ct = default);
    Task<List<FlatHistory>> GetHistoryAsync(CancellationToken ct = default);
    Task<List<FlatAggregation>> GetLatestAsync(CancellationToken ct = default);
}
