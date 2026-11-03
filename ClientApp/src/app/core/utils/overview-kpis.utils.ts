import { FlatAggregation } from '../models/flat-aggregation.model';
import { groupWeighted } from './aggregation.utils';

export interface DistrictPrice {
  district: string;
  avgPricePerMeter: number;
}

/** Key figures for the stat tiles above the Dashboard table. */
export interface OverviewKpis {
  flatCount: number;
  districtCount: number;
  /** Count-weighted over all rows. */
  avgPricePerMeter: number;
  cheapestDistrict: DistrictPrice | null;
  priciestDistrict: DistrictPrice | null;
}

/**
 * Key figures of the (filtered) aggregation rows, or `null` when they hold no flats. Rows without flats are
 * ignored; rows without a district count for the totals but not for the district figures.
 */
export function computeOverviewKpis(rows: readonly FlatAggregation[]): OverviewKpis | null {
  const withFlats = rows.filter(row => row.count > 0);
  if (withFlats.length === 0) {
    return null;
  }

  const total = groupWeighted(withFlats, () => 'all', () => ({ count: 0, avgPricePerMeter: 0 })).get('all')!;
  const byDistrict = Array.from(groupWeighted(
    withFlats.filter(row => row.district),
    row => row.district!,
    row => ({ district: row.district, count: 0, avgPricePerMeter: 0 })
  ).values())
    .map(row => ({ district: row.district!, avgPricePerMeter: row.avgPricePerMeter }))
    .sort((left, right) => left.avgPricePerMeter - right.avgPricePerMeter || left.district.localeCompare(right.district));

  return {
    flatCount: total.count,
    districtCount: byDistrict.length,
    avgPricePerMeter: total.avgPricePerMeter,
    cheapestDistrict: byDistrict[0] ?? null,
    priciestDistrict: byDistrict.length > 1 ? byDistrict[byDistrict.length - 1] : null
  };
}
