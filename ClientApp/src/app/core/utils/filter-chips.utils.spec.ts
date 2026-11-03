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
    it('lists districts, rooms, energy classes, then the maximum price and Provision, as in the toolbar', () => {
      const chips = buildFilterChips({
        districts: ['Mitte', 'Südstadt'], rooms: [2, 3.5], energyClasses: ['A+', 'B'], priceMax: 450000, commissionMax: 3.57
      });

      expect(chips).toEqual([
        { kind: 'district', value: 'Mitte', label: 'Mitte' },
        { kind: 'district', value: 'Südstadt', label: 'Südstadt' },
        { kind: 'rooms', value: 2, label: '2 rooms' },
        { kind: 'rooms', value: 3.5, label: '3,5 rooms' },
        { kind: 'energyClass', value: 'A+', label: 'EE class A+' },
        { kind: 'energyClass', value: 'B', label: 'EE class B' },
        // The euro format separates the currency with a non-breaking space
        { kind: 'priceMax', value: 450000, label: 'up to 450.000 €' },
        { kind: 'commissionMax', value: 3.57, label: 'Provision ≤ 3,57 %' }
      ]);
    });

    it('has no chips without active filters', () => {
      expect(buildFilterChips({})).toEqual([]);
      expect(buildFilterChips({ districts: [], rooms: [], energyClasses: [], priceMax: null, commissionMax: null })).toEqual([]);
    });

    it('ignores a price bound that filterByPriceRange ignores', () => {
      expect(buildFilterChips({ priceMax: 0 })).toEqual([]);
      expect(buildFilterChips({ priceMax: -5 })).toEqual([]);
    });

    it('shows a Provision bound of 0 (commission-free only) but not a negative one', () => {
      expect(buildFilterChips({ commissionMax: 0 })).toEqual([{ kind: 'commissionMax', value: 0, label: 'Provision ≤ 0 %' }]);
      expect(buildFilterChips({ commissionMax: -1 })).toEqual([]);
    });
  });

  it('keys a chip by its kind and value', () => {
    expect(filterChipKey({ kind: 'rooms', value: 2, label: '2 rooms' })).toBe('rooms:2');
    expect(filterChipKey({ kind: 'district', value: 'Mitte', label: 'Mitte' })).toBe('district:Mitte');
  });
});
