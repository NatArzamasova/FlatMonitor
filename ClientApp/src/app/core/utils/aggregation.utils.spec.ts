import { mergeWeightedAverage, roundPriceToEuro } from './aggregation.utils';

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

      mergeWeightedAverage(current, item);

      expect(current.count).toBe(15);
      expect(current.avgPricePerMeter).toBeCloseTo(
        (10 * 100 + 5 * 110) / 15,
        5
      );
    });

    it('should handle equal counts', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 10, avgPricePerMeter: 120 };

      mergeWeightedAverage(current, item);

      expect(current.count).toBe(20);
      expect(current.avgPricePerMeter).toBeCloseTo(110, 5);
    });

    it('should handle zero current count', () => {
      const current = { count: 0, avgPricePerMeter: 0 };
      const item = { count: 5, avgPricePerMeter: 100 };

      mergeWeightedAverage(current, item);

      expect(current.count).toBe(5);
      expect(current.avgPricePerMeter).toBeCloseTo(100, 5);
    });

    it('should handle large counts', () => {
      const current = { count: 1000, avgPricePerMeter: 150 };
      const item = { count: 500, avgPricePerMeter: 140 };

      mergeWeightedAverage(current, item);

      expect(current.count).toBe(1500);
      expect(current.avgPricePerMeter).toBeCloseTo(
        (1000 * 150 + 500 * 140) / 1500,
        5
      );
    });

    it('should handle decimal average prices', () => {
      const current = { count: 3, avgPricePerMeter: 12.5 };
      const item = { count: 2, avgPricePerMeter: 13.75 };

      mergeWeightedAverage(current, item);

      expect(current.count).toBe(5);
      expect(current.avgPricePerMeter).toBeCloseTo(
        (3 * 12.5 + 2 * 13.75) / 5,
        5
      );
    });

    it('should modify the current object in place', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const original = current;
      const item = { count: 5, avgPricePerMeter: 110 };

      mergeWeightedAverage(current, item);

      expect(current).toBe(original);
    });

    it('should not modify the item parameter', () => {
      const current = { count: 10, avgPricePerMeter: 100 };
      const item = { count: 5, avgPricePerMeter: 110 };
      const itemCopy = { ...item };

      mergeWeightedAverage(current, item);

      expect(item).toEqual(itemCopy);
    });
  });
});
