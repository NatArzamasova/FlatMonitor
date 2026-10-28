import { distinctSorted } from './collection.utils';

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
});
