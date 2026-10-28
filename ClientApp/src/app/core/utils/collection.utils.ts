/**
 * Distinct values without `null`, `undefined` and `''`, sorted with `compare`
 * (default: `Array.prototype.sort` order). `0` and `false` are kept.
 */
export function distinctSorted<T>(
  values: Iterable<T | null | undefined>,
  compare?: (left: T, right: T) => number
): T[] {
  const distinct = new Set<T>();
  for (const value of values) {
    if (value !== null && value !== undefined && value !== '') {
      distinct.add(value);
    }
  }
  return Array.from(distinct).sort(compare);
}

/** Whether both arrays contain the same values, ignoring order and duplicates. */
export function sameElements<T>(left: readonly T[], right: readonly T[]): boolean {
  const leftSet = new Set(left);
  const rightSet = new Set(right);
  return leftSet.size === rightSet.size && [...leftSet].every(value => rightSet.has(value));
}
