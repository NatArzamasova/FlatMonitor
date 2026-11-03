import { buildFilterChips, filterChipKey, roomsLabel } from './filter-chips.utils';

describe('filter chips', () => {
  describe('roomsLabel', () => {
    it('uses the singular for one room only', () => {
      expect(roomsLabel(1)).toBe('1 room');
      expect(roomsLabel(2)).toBe('2 rooms');
      expect(roomsLabel(2.5)).toBe('2,5 rooms');
    });
  });

  describe('buildFilterChips', () => {
    it('lists districts, then rooms, then the price bounds', () => {
      const chips = buildFilterChips({ districts: ['Mitte', 'Südstadt'], rooms: [2, 3.5], priceMin: 200000, priceMax: 450000 });

      expect(chips).toEqual([
        { kind: 'district', value: 'Mitte', label: 'Mitte' },
        { kind: 'district', value: 'Südstadt', label: 'Südstadt' },
        { kind: 'rooms', value: 2, label: '2 rooms' },
        { kind: 'rooms', value: 3.5, label: '3,5 rooms' },
        { kind: 'priceMin', value: 200000, label: 'from 200.000 €' },
        { kind: 'priceMax', value: 450000, label: 'up to 450.000 €' }
      ]);
    });

    it('has no chips without active filters', () => {
      expect(buildFilterChips({})).toEqual([]);
      expect(buildFilterChips({ districts: [], rooms: [], priceMin: null, priceMax: null })).toEqual([]);
    });

    it('ignores price bounds that filterByPriceRange ignores', () => {
      expect(buildFilterChips({ priceMin: 0, priceMax: -5 })).toEqual([]);
    });
  });

  it('keys a chip by its kind and value', () => {
    expect(filterChipKey({ kind: 'rooms', value: 2, label: '2 rooms' })).toBe('rooms:2');
    expect(filterChipKey({ kind: 'district', value: 'Mitte', label: 'Mitte' })).toBe('district:Mitte');
  });
});
