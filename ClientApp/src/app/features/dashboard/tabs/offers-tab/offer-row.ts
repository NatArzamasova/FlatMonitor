import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { SourceBadge } from '../../../../core/utils/source.utils';
import { EnergyClassBadge } from '../../../../core/utils/energy-class.utils';

export type OfferSortColumn =
  'district' | 'roomsCount' | 'priceEur' | 'livingAreaSqm' | 'pricePerMeter' | 'serviceChargeEur' | 'commissionPercent' | 'energyEfficiencyClass' | 'source';

/** An offer with its display state, shared by the table and the cards of the Current Offers tab. */
export interface OfferRow {
  offer: FlatOffer;
  discountPercentage: number | null;
  hasLink: boolean;
  source: SourceBadge;
  /** `null` when the offer has no class. */
  energyClass: EnergyClassBadge | null;
  /** The title photo shown by hovering over the ⓘ; `null` without one. */
  previewUrl: string | null;
}

/**
 * Opens the offer on its portal in a new tab, for a click or middle click (`auxclick`) anywhere on its table row or
 * card: a mouse shortcut to the source badge, which stays the real link (keyboard, "copy link"). Left alone: other
 * buttons (the right one opens the context menu), an offer without a usable URL, a click on a link or button inside
 * (the source link itself, the photo thumbnail), and a click that ends a text selection (copying a price).
 */
export function openOfferOnClick(row: OfferRow, event: MouseEvent): void {
  if (event.button !== 0 && event.button !== 1) {
    return;
  }
  if (!row.hasLink || (event.target as Element | null)?.closest('a, button')) {
    return;
  }
  if (window.getSelection()?.toString()) {
    return;
  }
  window.open(row.offer.url, '_blank', 'noopener,noreferrer');
}

/** `Open the offer on ImmobilienScout24`; the title of a clickable row or card and of its source link. */
export function openOfferLabel(row: OfferRow): string {
  return `Open the offer on ${row.offer.source || 'its portal'}`;
}

/** The sortable columns in table order, with the names used by the header buttons and the cards' sort control. */
export const OFFER_SORT_COLUMNS: readonly { column: OfferSortColumn; label: string }[] = [
  { column: 'district', label: 'District' },
  { column: 'roomsCount', label: 'Rooms' },
  { column: 'priceEur', label: 'Price' },
  { column: 'livingAreaSqm', label: 'Living Area' },
  { column: 'pricePerMeter', label: 'Price per Square Meter' },
  { column: 'serviceChargeEur', label: 'Hausgeld' },
  { column: 'commissionPercent', label: 'Provision' },
  { column: 'energyEfficiencyClass', label: 'Energy Efficiency Class' },
  { column: 'source', label: 'Source' }
];
