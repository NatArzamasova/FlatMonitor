import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { SourceBadge } from '../../../../core/utils/source.utils';

export type OfferSortColumn =
  'district' | 'roomsCount' | 'priceEur' | 'livingAreaSqm' | 'pricePerMeter' | 'source';

/** An offer with its display state, shared by the table and the cards of the Current Offers tab. */
export interface OfferRow {
  offer: FlatOffer;
  discountPercentage: number | null;
  hasLink: boolean;
  source: SourceBadge;
}

/** The sortable columns in table order, with the names used by the header buttons and the cards' sort control. */
export const OFFER_SORT_COLUMNS: readonly { column: OfferSortColumn; label: string }[] = [
  { column: 'district', label: 'District' },
  { column: 'roomsCount', label: 'Rooms' },
  { column: 'priceEur', label: 'Price' },
  { column: 'livingAreaSqm', label: 'Living Area' },
  { column: 'pricePerMeter', label: 'Price per Square Meter' },
  { column: 'source', label: 'Source' }
];
