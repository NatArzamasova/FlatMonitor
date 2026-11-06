import { formatDecimal, formatEuro, formatPercent } from './locale.utils';
import { isActiveMaximum } from './filter.utils';

/** An active filter shown as a removable chip under a tab's filter toolbar. */
export type FilterChip =
  | { kind: 'district'; value: string; label: string }
  | { kind: 'rooms'; value: number; label: string }
  | { kind: 'energyClass'; value: string; label: string }
  | { kind: 'priceMax' | 'commissionMax'; value: number; label: string }
  /** Archive tab: last seen at most `value` days ago. */
  | { kind: 'lastSeenWithin'; value: number; label: string };

export interface ActiveFilters {
  districts?: readonly string[];
  rooms?: readonly number[];
  /** Normalized classes (`A+` … `H`). */
  energyClasses?: readonly string[];
  priceMax?: number | null;
  /** Highest Provision in percent; `0` means commission-free only. */
  commissionMax?: number | null;
}


/** `1` → `1 room`, `2.5` → `2,5 rooms` (numbers in the app locale). */
export function roomsLabel(rooms: number): string {
  return `${formatDecimal(rooms)} ${rooms === 1 ? 'room' : 'rooms'}`;
}

/**
 * One chip per selected district, room count and energy class, then the maximum price and Provision, in the order of
 * the filter toolbar. The price bound counts only when it is `> 0` (as in `filterByPriceRange`), the Provision bound
 * also when it is `0` (as in `filterByMaximum`).
 */
export function buildFilterChips(filters: ActiveFilters): FilterChip[] {
  const chips: FilterChip[] = [
    ...(filters.districts ?? []).map(value => ({ kind: 'district' as const, value, label: value })),
    ...(filters.rooms ?? []).map(value => ({ kind: 'rooms' as const, value, label: roomsLabel(value) })),
    ...(filters.energyClasses ?? []).map(value => ({ kind: 'energyClass' as const, value, label: `EE class ${value}` }))
  ];
  if (filters.priceMax != null && filters.priceMax > 0) {
    chips.push({ kind: 'priceMax', value: filters.priceMax, label: `up to ${formatEuro(filters.priceMax)}` });
  }
  if (isActiveMaximum(filters.commissionMax)) {
    chips.push({ kind: 'commissionMax', value: filters.commissionMax, label: `Provision ≤ ${formatPercent(filters.commissionMax)}` });
  }
  return chips;
}

/** Identifies a chip within one toolbar, for `track`. */
export function filterChipKey(chip: FilterChip): string {
  return `${chip.kind}:${chip.value}`;
}
