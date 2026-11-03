const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

/** Short relative time for the past: `just now`, `5 min ago`, `2 h ago`, `1 day ago`, `3 days ago`. */
export function formatTimeAgo(date: Date, now: Date = new Date()): string {
  const elapsed = Math.max(0, now.getTime() - date.getTime());
  if (elapsed < MINUTE_MS) {
    return 'just now';
  }
  if (elapsed < HOUR_MS) {
    return `${Math.floor(elapsed / MINUTE_MS)} min ago`;
  }
  if (elapsed < DAY_MS) {
    return `${Math.floor(elapsed / HOUR_MS)} h ago`;
  }
  const days = Math.floor(elapsed / DAY_MS);
  return `${days} ${days === 1 ? 'day' : 'days'} ago`;
}
