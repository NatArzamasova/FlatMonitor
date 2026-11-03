import { FlatOffer } from '../models/flat-offer.model';

/**
 * The URL of the offer's title photo (`GET /api/flats/{source}/{listingId}/preview`), or `null` when the API has
 * none for it.
 */
export function offerPreviewUrl(offer: FlatOffer): string | null {
  if (!offer.hasPreview || !offer.source || !offer.listingId) {
    return null;
  }
  return `/api/flats/${encodeURIComponent(offer.source)}/${encodeURIComponent(offer.listingId)}/preview`;
}
