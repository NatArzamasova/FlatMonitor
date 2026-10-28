import { Aggregatable, groupWeighted, mergeWeightedAverage, roundPriceToEuro } from './aggregation.utils';

describe('aggregation.utils', () => {
  describe('roundPriceToEuro', () => {
    it('rounds average prices to the nearest whole euro', () => {
      expect(roundPriceToEuro(12.49)).toBe(12);
      expect(roundPriceToEuro(12.5)).toBe(13);
      expect(roundPriceToEuro(12.99)).toBe(13);
    });
  });

  describe('mergeWeightedAverage', () => {
    it('should merge two aggregations with correct weighted average', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 5, avgPricePerMeter: 110 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(15);
      expect(merged.avgPricePerMeter).toBeCloseTo(
        (10 * 100 + 5 * 110) / 15,
        5
      );
    });

    it('should handle equal counts', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 10, avgPricePerMeter: 120 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(20);
      expect(merged.avgPricePerMeter).toBeCloseTo(110, 5);
    });

    it('should handle zero current count', () => {
      const current = { count: 0, avgPricePerMeter: 0 };
      const item = { count: 5, avgPricePerMeter: 100 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(5);
      expect(merged.avgPricePerMeter).toBeCloseTo(100, 5);
    });

    it('should leave the aggregation unchanged when both counts are zero', () => {
      const current = { count: 0, avgPricePerMeter: 0 };
      const item = { count: 0, avgPricePerMeter: 100 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(0);
      expect(merged.avgPricePerMeter).toBe(0);
    });

    it('should ignore a zero-count item when merging into an existing aggregation', () => {
      const current = { count: 5, avgPricePerMeter: 100 };
      const item = { count: 0, avgPricePerMeter: 999 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(5);
      expect(merged.avgPricePerMeter).toBe(100);
    });

    it('should handle large counts', () => {
      const current = { count: 1000, avgPricePerMeter: 150 };
      const item = { count: 500, avgPricePerMeter: 140 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(1500);
      expect(merged.avgPricePerMeter).toBeCloseTo(
        (1000 * 150 + 500 * 140) / 1500,
        5
      );
    });

    it('should handle decimal average prices', () => {
      const current = { count: 3, avgPricePerMeter: 12.5 };
      const item = { count: 2, avgPricePerMeter: 13.75 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged.count).toBe(5);
      expect(merged.avgPricePerMeter).toBeCloseTo(
        (3 * 12.5 + 2 * 13.75) / 5,
        5
      );
    });

    it('should return a new object and leave the current one unchanged', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 5, avgPricePerMeter: 110 };

      const merged = mergeWeightedAverage(current, item);

      expect(merged).not.toBe(current);
      expect(current).toEqual({ count: 10, avgPricePerMeter: 100 });
    });

    it('should keep the other fields of the current object', () => {
      const current = { district: 'Mitte', count: 1, avgPricePerMeter: 10 };

      const merged = mergeWeightedAverage(current, { count: 1, avgPricePerMeter: 20 });

      expect(merged).toEqual({ district: 'Mitte', count: 2, avgPricePerMeter: 15 });
    });

    it('should not modify the item parameter', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 5, avgPricePerMeter: 110 };
      const itemCopy = { ...item };

      mergeWeightedAverage(current, item);

      expect(item).toEqual(itemCopy);
    });
  });

  describe('groupWeighted', () => {
    interface Row extends Aggregatable {
      group?: string;
      note?: string;
    }

    const rows: Row[] = [
      { group: 'b', note: 'first b', count: 1, avgPricePerMeter: 10 },
      { group: 'a', note: 'first a', count: 2, avgPricePerMeter: 20 },
      { group: 'b', note: 'second b', count: 3, avgPricePerMeter: 30 }
    ];
    const seed = (row: Row): Row => ({ group: row.group, note: row.note, count: 0, avgPricePerMeter: 0 });

    it('merges each group into one count-weighted row', () => {
      const groups = groupWeighted(rows, row => row.group, seed);

      expect(groups.get('a')).toEqual({ group: 'a', note: 'first a', count: 2, avgPricePerMeter: 20 });
      expect(groups.get('b')!.count).toBe(4);
      expect(groups.get('b')!.avgPricePerMeter).toBeCloseTo((10 + 90) / 4, 10);
    });

    it('seeds each group from its first row and keeps first-appearance order', () => {
      const groups = groupWeighted(rows, row => row.group, seed);

      expect(Array.from(groups.keys())).toEqual(['b', 'a']);
      expect(groups.get('b')!.note).toBe('first b');
    });

    it('treats undefined as its own key', () => {
      const groups = groupWeighted([...rows, { count: 1, avgPricePerMeter: 5 }], row => row.group, seed);

      expect(groups.get(undefined)).toEqual({ group: undefined, note: undefined, count: 1, avgPricePerMeter: 5 });
    });

    it('returns an empty map for no rows', () => {
      expect(groupWeighted([], (row: Row) => row.group, seed).size).toBe(0);
    });

    it('does not modify the input rows', () => {
      const copy = rows.map(row => ({ ...row }));

      groupWeighted(rows, row => row.group, seed);

      expect(rows).toEqual(copy);
    });
  });
});
