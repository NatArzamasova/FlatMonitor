import { computeOverviewKpis } from './overview-kpis.utils';

describe('computeOverviewKpis', () => {
  it('sums the flats and weights the average price by the flat count', () => {
    const kpis = computeOverviewKpis([
      { district: 'Mitte', roomsCount: 2, count: 3, avgPricePerMeter: 4000 },
      { district: 'Mitte', roomsCount: 3, count: 1, avgPricePerMeter: 6000 },
      { district: 'Durlach', roomsCount: 2, count: 4, avgPricePerMeter: 3000 }
    ])!;

    expect(kpis.flatCount).toBe(8);
    expect(kpis.districtCount).toBe(2);
    expect(kpis.avgPricePerMeter).toBe(3750);
  });

  it('finds the cheapest and the priciest district by their weighted average', () => {
    const kpis = computeOverviewKpis([
      { district: 'Mitte', roomsCount: 2, count: 3, avgPricePerMeter: 4000 },
      { district: 'Mitte', roomsCount: 3, count: 1, avgPricePerMeter: 6000 },
      { district: 'Durlach', roomsCount: 2, count: 4, avgPricePerMeter: 3000 },
      { district: 'Südstadt', roomsCount: 2, count: 2, avgPricePerMeter: 5000 }
    ])!;

    expect(kpis.cheapestDistrict).toEqual({ district: 'Durlach', avgPricePerMeter: 3000 });
    expect(kpis.priciestDistrict).toEqual({ district: 'Südstadt', avgPricePerMeter: 5000 });
  });

  it('has no priciest district when only one district is left', () => {
    const kpis = computeOverviewKpis([{ district: 'Mitte', roomsCount: 2, count: 3, avgPricePerMeter: 4000 }])!;

    expect(kpis.cheapestDistrict).toEqual({ district: 'Mitte', avgPricePerMeter: 4000 });
    expect(kpis.priciestDistrict).toBeNull();
  });

  it('counts rows without a district for the totals only', () => {
    const kpis = computeOverviewKpis([
      { roomsCount: 2, count: 2, avgPricePerMeter: 5000 },
      { district: 'Mitte', roomsCount: 2, count: 2, avgPricePerMeter: 3000 }
    ])!;

    expect(kpis.flatCount).toBe(4);
    expect(kpis.avgPricePerMeter).toBe(4000);
    expect(kpis.districtCount).toBe(1);
    expect(kpis.cheapestDistrict!.district).toBe('Mitte');
  });

  it('returns null without flats', () => {
    expect(computeOverviewKpis([])).toBeNull();
    expect(computeOverviewKpis([{ district: 'Mitte', count: 0, avgPricePerMeter: 0 }])).toBeNull();
  });
});
