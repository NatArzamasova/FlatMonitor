import { filterByDistricts, filterByPriceRange, filterByRooms } from './filter.utils';

describe('filter.utils', () => {
  describe('filterByDistricts', () => {
    const testData = [
      { district: 'Mitte', value: 10 },
      { district: 'Charlottenburg', value: 20 },
      { district: 'Kreuzberg', value: 30 },
      { district: undefined, value: 40 }
    ];

    it('should return all data when filter is empty', () => {
      const result = filterByDistricts(testData, [], item => item.district);
      expect(result).toEqual(testData);
    });

    it('should filter by single district', () => {
      const result = filterByDistricts(testData, ['Mitte'], item => item.district);
      expect(result.length).toBe(1);
      expect(result[0].district).toBe('Mitte');
    });

    it('should filter by multiple districts', () => {
      const result = filterByDistricts(testData, ['Mitte', 'Kreuzberg'], item => item.district);
      expect(result.length).toBe(2);
      expect(result.map(d => d.district)).toContain('Mitte');
      expect(result.map(d => d.district)).toContain('Kreuzberg');
    });

    it('should return empty array when no matches', () => {
      const result = filterByDistricts(testData, ['NonExistent'], item => item.district);
      expect(result.length).toBe(0);
    });

    it('should handle undefined districts', () => {
      const result = filterByDistricts(testData, [''], item => item.district);
      expect(result.length).toBe(1);
      expect(result[0].value).toBe(40);
    });
  });

  describe('filterByRooms', () => {
    const testData = [
      { rooms: 1, value: 10 },
      { rooms: 2, value: 20 },
      { rooms: 3, value: 30 },
      { rooms: null, value: 40 },
      { rooms: undefined, value: 50 }
    ];

    it('should return all data when filter is empty', () => {
      const result = filterByRooms(testData, [], item => item.rooms);
      expect(result).toEqual(testData);
    });

    it('should filter by single room count', () => {
      const result = filterByRooms(testData, [2], item => item.rooms);
      expect(result.length).toBe(1);
      expect(result[0].rooms).toBe(2);
    });

    it('should filter by multiple room counts', () => {
      const result = filterByRooms(testData, [1, 3], item => item.rooms);
      expect(result.length).toBe(2);
      expect(result.map(d => d.rooms)).toContain(1);
      expect(result.map(d => d.rooms)).toContain(3);
    });

    it('should exclude undefined and null values', () => {
      const result = filterByRooms(testData, [1, 2, 3], item => item.rooms);
      expect(result.length).toBe(3);
      expect(result.every(d => d.rooms !== null && d.rooms !== undefined)).toBe(true);
    });

    it('should return empty array when no matches', () => {
      const result = filterByRooms(testData, [99], item => item.rooms);
      expect(result.length).toBe(0);
    });
  });

  describe('filterByPriceRange', () => {
    const testData = [
      { price: 1000, value: 10 },
      { price: 2000, value: 20 },
      { price: 3000, value: 30 },
      { price: undefined, value: 40 }
    ];

    it('should return all data, including items without a price, when both min and max are null', () => {
      const result = filterByPriceRange(testData, null, null, item => item.price);
      expect(result).toEqual(testData);
    });

    it('should filter by minimum price', () => {
      const result = filterByPriceRange(testData, 1500, null, item => item.price);
      expect(result.length).toBe(2);
      expect(result.map(d => d.price)).toContain(2000);
      expect(result.map(d => d.price)).toContain(3000);
    });

    it('should filter by maximum price', () => {
      const result = filterByPriceRange(testData, null, 2500, item => item.price);
      expect(result.length).toBe(2);
      expect(result.map(d => d.price)).toContain(1000);
      expect(result.map(d => d.price)).toContain(2000);
    });

    it('should filter by both min and max price', () => {
      const result = filterByPriceRange(testData, 1500, 2500, item => item.price);
      expect(result.length).toBe(1);
      expect(result[0].price).toBe(2000);
    });

    it('should ignore min/max when set to 0 or less', () => {
      const result = filterByPriceRange(testData, 0, -1, item => item.price);
      expect(result).toEqual(testData);
    });

    it('should exclude items without a price when a minimum is set', () => {
      const result = filterByPriceRange(testData, 500, null, item => item.price);
      expect(result.map(d => d.price)).toEqual([1000, 2000, 3000]);
    });

    it('should exclude items without a price when a maximum is set', () => {
      const result = filterByPriceRange(testData, null, 5000, item => item.price);
      expect(result.map(d => d.price)).toEqual([1000, 2000, 3000]);
    });
  });
});
