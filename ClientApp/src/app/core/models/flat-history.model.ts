/** Mirrors the backend's FlatHistory: price and count are always sent, the rest may be null. */
export interface FlatHistory {
  scrapingDate?: string | null;
  district?: string | null;
  roomsCount?: number | null;
  avgPricePerMeter: number;
  count: number;
}
