import { normalizeRooms } from './rooms.utils';

describe('rooms.utils', () => {
  describe('normalizeRooms', () => {
    it('keeps whole and half room counts unchanged', () => {
      expect(normalizeRooms(2)).toBe(2);
      expect(normalizeRooms(2.5)).toBe(2.5);
    });

    it('rounds to one decimal place', () => {
      expect(normalizeRooms(2.54)).toBe(2.5);
      expect(normalizeRooms(2.56)).toBe(2.6);
      expect(normalizeRooms(3.333)).toBe(3.3);
    });

    it('returns undefined for missing or non-finite values', () => {
      expect(normalizeRooms(undefined)).toBeUndefined();
      expect(normalizeRooms(null)).toBeUndefined();
      expect(normalizeRooms(Number.NaN)).toBeUndefined();
    });
  });
});
