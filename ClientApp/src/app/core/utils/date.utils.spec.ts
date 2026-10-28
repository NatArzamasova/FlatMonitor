import { parseUtcDate } from './date.utils';

describe('date.utils', () => {
  describe('parseUtcDate', () => {
    it('treats a date-time without offset as UTC', () => {
      expect(parseUtcDate('2024-01-15T10:00:00').toISOString()).toBe('2024-01-15T10:00:00.000Z');
      expect(parseUtcDate('2024-01-15T10:00:00.1234567').toISOString()).toBe('2024-01-15T10:00:00.123Z');
    });

    it('keeps an explicit UTC or numeric offset', () => {
      expect(parseUtcDate('2024-01-15T10:00:00Z').toISOString()).toBe('2024-01-15T10:00:00.000Z');
      expect(parseUtcDate('2024-01-15T10:00:00+01:00').toISOString()).toBe('2024-01-15T09:00:00.000Z');
      expect(parseUtcDate('2024-01-15T10:00:00-0500').toISOString()).toBe('2024-01-15T15:00:00.000Z');
    });

    it('parses date-only strings as UTC midnight', () => {
      expect(parseUtcDate('2024-01-15').toISOString()).toBe('2024-01-15T00:00:00.000Z');
    });
  });
});
