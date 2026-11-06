import { ArchivedOffer } from '../models/archived-offer.model';

const DAY_MS = 24 * 60 * 60 * 1000;

/** The choices of the Archive tab's "Last seen" filter, in days; `null` is all. */
export const LAST_SEEN_PERIODS: readonly { days: number | null; label: string }[] = [
  { days: 28, label: 'last 4 weeks' },
  { days: 91, label: 'last 3 months' },
  { days: 365, label: 'last year' },
  { days: null, label: 'all' }
];

/** `91` → `last 3 months`; a number not among the choices → `last 91 days`. */
export function lastSeenPeriodLabel(days: number): string {
  return LAST_SEEN_PERIODS.find(period => period.days === days)?.label ?? `last ${days} days`;
}

/**
 * The whole days between the first and the last time the flat was seen: `0` for a flat seen by one scrape only.
 * `null` when a date is missing or unreadable.
 */
export function daysListed(offer: Pick<ArchivedOffer, 'firstSeenUtc' | 'lastSeenUtc'>): number | null {
  const first = Date.parse(offer.firstSeenUtc);
  const last = Date.parse(offer.lastSeenUtc);
  if (Number.isNaN(first) || Number.isNaN(last)) {
    return null;
  }
  return Math.max(0, Math.floor((last - first) / DAY_MS));
}

/** Keeps the offers last seen at most `days` days before `now`; `null` keeps everything. */
export function filterByLastSeen<T extends Pick<ArchivedOffer, 'lastSeenUtc'>>(
  offers: T[],
  days: number | null,
  now: Date = new Date()
): T[] {
  if (days === null) {
    return offers;
  }
  const since = now.getTime() - days * DAY_MS;
  return offers.filter(offer => Date.parse(offer.lastSeenUtc) >= since);
}
