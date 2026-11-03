export type SortDirection = 'asc' | 'desc';

/**
 * Click-to-sort state for a table: the first click on a column sorts ascending, the next one toggles.
 * Missing values always sort last; numbers compare numerically, everything else with a
 * case-insensitive, digit-aware `localeCompare`. A column whose raw values do not sort meaningfully can get its own
 * sort value: `new TableSort<Offer, Column>({ energyClass: offer => rank(offer.energyClass) })`.
 */
export class TableSort<T, K extends keyof T = keyof T> {
  column: K | null = null;
  direction: SortDirection = 'asc';

  constructor(private readonly sortValues: Partial<Record<K, (row: T) => unknown>> = {}) {}

  toggle(column: K): void {
    if (this.column === column) {
      this.direction = this.direction === 'asc' ? 'desc' : 'asc';
    } else {
      this.column = column;
      this.direction = 'asc';
    }
  }

  indicator(column: K): string {
    return this.column === column ? (this.direction === 'asc' ? '↑' : '↓') : '';
  }

  ariaSort(column: K): 'ascending' | 'descending' | null {
    return this.column === column
      ? (this.direction === 'asc' ? 'ascending' : 'descending')
      : null;
  }

  /** Returns a sorted copy (or a plain copy when no column is selected). */
  apply(rows: readonly T[]): T[] {
    const column = this.column;
    if (column === null) {
      return [...rows];
    }

    const direction = this.direction === 'asc' ? 1 : -1;
    const valueOf = this.sortValues[column] ?? ((row: T) => row[column]);
    return [...rows].sort((left, right) => {
      const leftValue = valueOf(left);
      const rightValue = valueOf(right);
      if (leftValue == null) return rightValue == null ? 0 : 1;
      if (rightValue == null) return -1;
      const comparison = typeof leftValue === 'number' && typeof rightValue === 'number'
        ? leftValue - rightValue
        : String(leftValue).localeCompare(String(rightValue), undefined, { numeric: true, sensitivity: 'base' });
      return comparison * direction;
    });
  }
}
