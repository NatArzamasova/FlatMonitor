import { formatTimeAgo } from './relative-time.utils';

describe('formatTimeAgo', () => {
  const now = new Date('2026-11-03T12:00:00Z');
  const ago = (ms: number) => formatTimeAgo(new Date(now.getTime() - ms), now);

  it('says "just now" below a minute and for times in the future', () => {
    expect(ago(0)).toBe('just now');
    expect(ago(59_000)).toBe('just now');
    expect(ago(-5_000)).toBe('just now');
  });

  it('counts whole minutes below an hour', () => {
    expect(ago(60_000)).toBe('1 min ago');
    expect(ago(59 * 60_000 + 59_000)).toBe('59 min ago');
  });

  it('counts whole hours below a day', () => {
    expect(ago(60 * 60_000)).toBe('1 h ago');
    expect(ago(23 * 60 * 60_000 + 59 * 60_000)).toBe('23 h ago');
  });

  it('counts whole days from a day on', () => {
    expect(ago(24 * 60 * 60_000)).toBe('1 day ago');
    expect(ago(3 * 24 * 60 * 60_000 + 5 * 60 * 60_000)).toBe('3 days ago');
  });
});
