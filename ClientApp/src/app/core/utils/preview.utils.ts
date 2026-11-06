import { FlatOffer } from '../models/flat-offer.model';

/**
 * The URL of the offer's title photo (`GET /api/flats/{source}/{listingId}/preview`), or `null` when the API has
 * none for it.
 */
export function offerPreviewUrl(offer: FlatOffer): string | null {
  return photoUrl(offer, 'preview');
}

/**
 * The URL of an archived offer's photo, the one it was first seen with
 * (`GET /api/flats/{source}/{listingId}/archived-preview`), or `null` when the archive has none for it.
 */
export function archivedPreviewUrl(offer: FlatOffer): string | null {
  return photoUrl(offer, 'archived-preview');
}

function photoUrl(offer: FlatOffer, endpoint: 'preview' | 'archived-preview'): string | null {
  if (!offer.hasPreview || !offer.source || !offer.listingId) {
    return null;
  }
  return `/api/flats/${encodeURIComponent(offer.source)}/${encodeURIComponent(offer.listingId)}/${endpoint}`;
}
