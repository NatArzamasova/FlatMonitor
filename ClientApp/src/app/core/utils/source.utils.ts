/** How a flat's source is shown: a short label and the class that colours its pill (see `styles.scss`). */
export interface SourceBadge {
  label: string;
  className: string;
}

// Keys are the backend's source names (`SourceName` of each scraper).
const SOURCE_BADGES: Readonly<Record<string, SourceBadge>> = {
  ImmobilienScout24: { label: 'IS24', className: 'source-is24' },
  Immowelt: { label: 'Immowelt', className: 'source-immowelt' },
  OhneMakler: { label: 'ohne-makler', className: 'source-ohne-makler' }
};

/** The badge of a known source; an unknown one keeps its name with a neutral colour, a missing one shows `-`. */
export function sourceBadge(source: string | undefined): SourceBadge {
  if (!source) {
    return { label: '-', className: 'source-other' };
  }
  return SOURCE_BADGES[source] ?? { label: source, className: 'source-other' };
}
