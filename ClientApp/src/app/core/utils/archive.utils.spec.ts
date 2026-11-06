import { LAST_SEEN_PERIODS, daysListed, filterByLastSeen, lastSeenPeriodLabel } from './archive.utils';

describe('archive.utils', () => {
  describe('daysListed', () => {
    it('counts the whole days between first and last seen', () => {
      expect(daysListed({ firstSeenUtc: '2026-10-02T08:00:00Z', lastSeenUtc: '2026-10-29T07:59:59Z' })).toBe(26);
      expect(daysListed({ firstSeenUtc: '2026-10-02T08:00:00Z', lastSeenUtc: '2026-10-29T08:00:00Z' })).toBe(27);
    });

    it('is 0 for a flat seen by one scrape only, and never negative', () => {
      expect(daysListed({ firstSeenUtc: '2026-10-02T08:00:00Z', lastSeenUtc: '2026-10-02T08:30:00Z' })).toBe(0);
      expect(daysListed({ firstSeenUtc: '2026-10-03T08:00:00Z', lastSeenUtc: '2026-10-02T08:00:00Z' })).toBe(0);
    });

    it('is null for a missing or unreadable date', () => {
      expect(daysListed({ firstSeenUtc: '', lastSeenUtc: '2026-10-02T08:00:00Z' })).toBeNull();
      expect(daysListed({ firstSeenUtc: '2026-10-02T08:00:00Z', lastSeenUtc: 'soon' })).toBeNull();
    });
  });

  describe('filterByLastSeen', () => {
    const now = new Date('2026-11-06T12:00:00Z');
    const offers = [
      { id: 'yesterday', lastSeenUtc: '2026-11-05T12:00:00Z' },
      { id: 'four weeks', lastSeenUtc: '2026-10-09T12:00:00Z' },
      { id: 'two months', lastSeenUtc: '2026-09-06T12:00:00Z' }
    ];

    it('keeps the offers last seen within the days, the bound included', () => {
      expect(filterByLastSeen(offers, 28, now).map(offer => offer.id)).toEqual(['yesterday', 'four weeks']);
      expect(filterByLastSeen(offers, 1, now).map(offer => offer.id)).toEqual(['yesterday']);
    });

    it('keeps everything without a period', () => {
      expect(filterByLastSeen(offers, null, now)).toBe(offers);
    });
  });

  describe('lastSeenPeriodLabel', () => {
    it('names the choices of the filter, other numbers in days', () => {
      expect(lastSeenPeriodLabel(91)).toBe('last 3 months');
      expect(lastSeenPeriodLabel(10)).toBe('last 10 days');
    });

    it('offers all as the last choice', () => {
      expect(LAST_SEEN_PERIODS.map(period => period.days)).toEqual([28, 91, 365, null]);
    });
  });
});
