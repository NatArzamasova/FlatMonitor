export interface Aggregatable {
  count: number;
  avgPricePerMeter: number;
}

export function mergeWeightedAverage(
  current: Aggregatable,
  item: Aggregatable
): void {
  const oldCount = current.count;
  const newTotalCount = oldCount + item.count;
  current.avgPricePerMeter =
    (oldCount * current.avgPricePerMeter + item.count * item.avgPricePerMeter) /
    newTotalCount;
  current.count = newTotalCount;
}
