import { formatDecimal, formatEuro } from './locale.utils';

/** An active filter shown as a removable chip under a tab's filter toolbar. */
export type FilterChip =
  | { kind: 'district'; value: string; label: string }
  | { kind: 'rooms'; value: number; label: string }
  | { kind: 'priceMin' | 'priceMax'; value: number; label: string };

export interface ActiveFilters {
  districts?: readonly string[];
  rooms?: readonly number[];
  priceMin?: number | null;
  priceMax?: number | null;
}


/** `1` → `1 room`, `2.5` → `2,5 rooms` (numbers in the app locale). */
export function roomsLabel(rooms: number): string {
  return `${formatDecimal(rooms)} ${rooms === 1 ? 'room' : 'rooms'}`;
}

/**
 * One chip per selected district and room count, then the price bounds. A price bound counts only when it is
 * `> 0`, as in `filterByPriceRange`.
 */
export function buildFilterChips(filters: ActiveFilters): FilterChip[] {
  const chips: FilterChip[] = [
    ...(filters.districts ?? []).map(value => ({ kind: 'district' as const, value, label: value })),
    ...(filters.rooms ?? []).map(value => ({ kind: 'rooms' as const, value, label: roomsLabel(value) }))
  ];
  if (filters.priceMin != null && filters.priceMin > 0) {
    chips.push({ kind: 'priceMin', value: filters.priceMin, label: `from ${formatEuro(filters.priceMin)}` });
  }
  if (filters.priceMax != null && filters.priceMax > 0) {
    chips.push({ kind: 'priceMax', value: filters.priceMax, label: `up to ${formatEuro(filters.priceMax)}` });
  }
  return chips;
}

/** Identifies a chip within one toolbar, for `track`. */
export function filterChipKey(chip: FilterChip): string {
  return `${chip.kind}:${chip.value}`;
}
