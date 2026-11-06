import { FlatOffer } from './flat-offer.model';

/**
 * A flat that is no longer offered (`GET /api/flats/archived-offers`): its values are those of the last scrape that
 * found it; `hasPreview` refers to its archived photo, see `archivedPreviewUrl()`.
 */
export interface ArchivedOffer extends FlatOffer {
  /** When the flat was first seen, ISO 8601 in UTC (`2026-10-02T07:31:59Z`). */
  firstSeenUtc: string;
  /** When the flat was last seen, ISO 8601 in UTC. */
  lastSeenUtc: string;
  /**
   * The price when the flat was first seen; with `priceEur` (the last one) it shows how the price changed while
   * it was online. `originalPriceEur` is the portal's own original price.
   */
  firstPriceEur?: number;
}
