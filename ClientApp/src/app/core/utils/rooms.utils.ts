export function normalizeRooms(rooms: number | undefined | null): number | undefined {
  if (rooms === undefined || rooms === null || !Number.isFinite(rooms)) {
    return undefined;
  }
  return Math.round(rooms * 10) / 10;
}
