export interface Aggregatable {
  count: number;
  avgPricePerMeter: number;
}

export function roundPriceToEuro(pricePerMeter: number): number {
  return Math.round(pricePerMeter);
}

/** Returns `current` with `item` merged in (count-weighted average); `current` is not modified. */
export function mergeWeightedAverage<T extends Aggregatable>(current: T, item: Aggregatable): T {
  const totalCount = current.count + item.count;
  if (totalCount === 0) {
    return current;
  }
  return {
    ...current,
    count: totalCount,
    avgPricePerMeter: (current.count * current.avgPricePerMeter + item.count * item.avgPricePerMeter) / totalCount
  };
}

/**
 * Groups rows by `keyOf` and merges each group into one count-weighted row. `seed` builds a group's
 * starting row (count 0) from its first member. Groups keep the order in which their keys first appear.
 */
export function groupWeighted<T extends Aggregatable, K>(
  rows: readonly T[],
  keyOf: (row: T) => K,
  seed: (row: T) => T
): Map<K, T> {
  const groups = new Map<K, T>();
  for (const row of rows) {
    const key = keyOf(row);
    groups.set(key, mergeWeightedAverage(groups.get(key) ?? seed(row), row));
  }
  return groups;
}
