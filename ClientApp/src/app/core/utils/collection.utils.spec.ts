import { distinctSorted, sameElements } from './collection.utils';

describe('collection.utils', () => {
  describe('distinctSorted', () => {
    it('removes duplicates and sorts strings', () => {
      expect(distinctSorted(['Weststadt', 'Mitte', 'Weststadt', 'Durlach'])).toEqual(['Durlach', 'Mitte', 'Weststadt']);
    });

    it('drops null, undefined and empty strings', () => {
      expect(distinctSorted(['Mitte', null, undefined, '', 'Mitte'])).toEqual(['Mitte']);
    });

    it('keeps zero and sorts numbers with the given comparison', () => {
      expect(distinctSorted([10, 2, 0, undefined, 2.5, 2], (a, b) => a - b)).toEqual([0, 2, 2.5, 10]);
    });

    it('accepts any iterable', () => {
      expect(distinctSorted(new Set(['b', 'a']))).toEqual(['a', 'b']);
    });
  });

  describe('sameElements', () => {
    it('is true for the same values in a different order', () => {
      expect(sameElements(['a', 'b'], ['b', 'a'])).toBeTrue();
    });

    it('ignores duplicates', () => {
      expect(sameElements(['a', 'a', 'b'], ['b', 'a'])).toBeTrue();
    });

    it('is true for two empty arrays', () => {
      expect(sameElements([], [])).toBeTrue();
    });

    it('is false when a value is missing', () => {
      expect(sameElements(['a', 'b'], ['a'])).toBeFalse();
      expect(sameElements(['a'], ['a', 'b'])).toBeFalse();
    });

    it('is false for different values of the same count', () => {
      expect(sameElements(['a', 'b'], ['a', 'c'])).toBeFalse();
    });
  });
});
