/**
 * How a flat's energy efficiency class is shown: the label of the German energy certificate scale and the class that
 * colours its pill like the scale (dark green A+ … red H; see `.ee-*` in `styles.scss`). `rank` orders the classes
 * from best (0 = A+) to worst, for sorting.
 */
export interface EnergyClassBadge {
  label: string;
  className: string;
  rank: number | undefined;
}

/** Best to worst, as on the certificate (GEG 2020). */
export const ENERGY_CLASSES = ['A+', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'] as const;

const CLASS_NAMES: Readonly<Record<(typeof ENERGY_CLASSES)[number], string>> = {
  'A+': 'ee-a-plus', A: 'ee-a', B: 'ee-b', C: 'ee-c', D: 'ee-d', E: 'ee-e', F: 'ee-f', G: 'ee-g', H: 'ee-h'
};

/**
 * Normalizes the portals' spellings: ImmobilienScout24 sends `A_PLUS`, Immowelt `A+`, the others `A` … `H`
 * (any case, with spaces). Returns `undefined` for anything else.
 */
export function normalizeEnergyClass(value: string | null | undefined): (typeof ENERGY_CLASSES)[number] | undefined {
  const normalized = (value ?? '').trim().toUpperCase().replace(/\s+/g, '').replace(/_?PLUS$/, '+');
  return ENERGY_CLASSES.find(energyClass => energyClass === normalized);
}

/** 0 for A+ … 8 for H; `undefined` for a missing or unknown class (sorts last). */
export function energyClassRank(value: string | null | undefined): number | undefined {
  const energyClass = normalizeEnergyClass(value);
  return energyClass === undefined ? undefined : ENERGY_CLASSES.indexOf(energyClass);
}

/** A known class in its scale colour; an unknown one keeps its text with a neutral pill, a missing one shows `-`. */
export function energyClassBadge(value: string | null | undefined): EnergyClassBadge | null {
  if (!value?.trim()) {
    return null;
  }
  const energyClass = normalizeEnergyClass(value);
  return energyClass === undefined
    ? { label: value.trim(), className: 'ee-other', rank: undefined }
    : { label: energyClass, className: CLASS_NAMES[energyClass], rank: ENERGY_CLASSES.indexOf(energyClass) };
}
