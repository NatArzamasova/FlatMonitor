import { TableSort } from './table-sort';

interface Row {
  name?: string;
  size?: number;
}

describe('TableSort', () => {
  const rows: Row[] = [
    { name: 'b', size: 10 },
    { name: 'A', size: 2 },
    { name: undefined, size: undefined },
    { name: 'c', size: 1 }
  ];

  let sort: TableSort<Row, 'name' | 'size'>;

  beforeEach(() => {
    sort = new TableSort<Row, 'name' | 'size'>();
  });

  describe('toggle', () => {
    it('sorts a newly selected column ascending', () => {
      sort.toggle('name');

      expect(sort.column).toBe('name');
      expect(sort.direction).toBe('asc');
    });

    it('toggles the direction when the same column is selected again', () => {
      sort.toggle('name');
      sort.toggle('name');
      expect(sort.direction).toBe('desc');

      sort.toggle('name');
      expect(sort.direction).toBe('asc');
    });

    it('resets to ascending when switching to another column', () => {
      sort.toggle('name');
      sort.toggle('name');

      sort.toggle('size');

      expect(sort.column).toBe('size');
      expect(sort.direction).toBe('asc');
    });
  });

  describe('indicator and ariaSort', () => {
    it('are empty for unsorted columns', () => {
      sort.toggle('name');

      expect(sort.indicator('size')).toBe('');
      expect(sort.ariaSort('size')).toBeNull();
    });

    it('reflect the direction of the sorted column', () => {
      sort.toggle('name');
      expect(sort.indicator('name')).toBe('↑');
      expect(sort.ariaSort('name')).toBe('ascending');

      sort.toggle('name');
      expect(sort.indicator('name')).toBe('↓');
      expect(sort.ariaSort('name')).toBe('descending');
    });
  });

  describe('apply', () => {
    it('returns an unsorted copy when no column is selected', () => {
      const result = sort.apply(rows);

      expect(result).toEqual(rows);
      expect(result).not.toBe(rows);
    });

    it('sorts numbers numerically, not as strings', () => {
      sort.toggle('size');

      expect(sort.apply(rows).map(row => row.size)).toEqual([1, 2, 10, undefined]);
    });

    it('sorts strings case-insensitively', () => {
      sort.toggle('name');

      expect(sort.apply(rows).map(row => row.name)).toEqual(['A', 'b', 'c', undefined]);
    });

    it('sorts numeric strings by value', () => {
      const labels = [{ name: 'Flat 10' }, { name: 'Flat 9' }];
      const labelSort = new TableSort<Row, 'name'>();
      labelSort.toggle('name');

      expect(labelSort.apply(labels).map(row => row.name)).toEqual(['Flat 9', 'Flat 10']);
    });

    it('keeps missing values last in both directions', () => {
      sort.toggle('size');
      sort.toggle('size');

      expect(sort.apply(rows).map(row => row.size)).toEqual([10, 2, 1, undefined]);
    });

    it('does not modify the input', () => {
      const snapshot = [...rows];
      sort.toggle('size');

      sort.apply(rows);

      expect(rows).toEqual(snapshot);
    });
  });
});
