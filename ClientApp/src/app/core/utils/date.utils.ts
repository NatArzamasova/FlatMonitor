// "Z" or "+01:00" / "-0500" at the end of an ISO date-time.
const TIME_ZONE_SUFFIX = /(Z|[+-]\d{2}:?\d{2})$/i;

/**
 * Parses an ISO date-time that is known to be UTC. The backend reads its timestamps from SQLite
 * without a DateTimeKind, so they are serialized without an offset; `new Date()` would treat such a
 * string as local time. Strings that already carry an offset are parsed unchanged.
 */
export function parseUtcDate(value: string): Date {
  const isDateTime = value.includes('T');
  return new Date(isDateTime && !TIME_ZONE_SUFFIX.test(value) ? `${value}Z` : value);
}
