import { energyClassBadge, energyClassRank, ENERGY_CLASSES, normalizeEnergyClass } from './energy-class.utils';

describe('energy-class.utils', () => {
  describe('normalizeEnergyClass', () => {
    it('accepts the spellings of every portal', () => {
      expect(normalizeEnergyClass('A_PLUS')).toBe('A+');
      expect(normalizeEnergyClass('A+')).toBe('A+');
      expect(normalizeEnergyClass(' a plus ')).toBe('A+');
      expect(normalizeEnergyClass('d')).toBe('D');
      expect(normalizeEnergyClass('H')).toBe('H');
    });

    it('returns undefined for missing or unknown classes', () => {
      expect(normalizeEnergyClass(undefined)).toBeUndefined();
      expect(normalizeEnergyClass('')).toBeUndefined();
      expect(normalizeEnergyClass('A++')).toBeUndefined();
      expect(normalizeEnergyClass('I')).toBeUndefined();
    });
  });

  describe('energyClassRank', () => {
    it('ranks the classes from A+ (best) to H', () => {
      expect(['H', 'A', 'A_PLUS', 'C'].map(energyClassRank)).toEqual([8, 1, 0, 3]);
      expect(energyClassRank('unknown')).toBeUndefined();
    });
  });

  describe('energyClassBadge', () => {
    it('gives each known class its scale colour', () => {
      expect(ENERGY_CLASSES.map(value => energyClassBadge(value)!.className))
        .toEqual(['ee-a-plus', 'ee-a', 'ee-b', 'ee-c', 'ee-d', 'ee-e', 'ee-f', 'ee-g', 'ee-h']);
      expect(energyClassBadge('A_PLUS')).toEqual({ label: 'A+', className: 'ee-a-plus', rank: 0 });
    });

    it('keeps an unknown class with a neutral pill and shows nothing for a missing one', () => {
      expect(energyClassBadge('A++')).toEqual({ label: 'A++', className: 'ee-other', rank: undefined });
      expect(energyClassBadge(undefined)).toBeNull();
      expect(energyClassBadge('  ')).toBeNull();
    });

    it('colours the pills with readable text (at least 4.5:1) in both themes', () => {
      for (const theme of ['light', 'dark']) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        for (const value of ENERGY_CLASSES) {
          const pill = document.createElement('span');
          pill.className = `badge ee-badge ${energyClassBadge(value)!.className}`;
          document.body.appendChild(pill);
          const style = getComputedStyle(pill);
          expect(contrastRatio(style.color, style.backgroundColor)).withContext(`${theme} ${value}`).toBeGreaterThanOrEqual(4.5);
          pill.remove();
        }
      }
      document.documentElement.removeAttribute('data-bs-theme');
    });
  });
});

/** Contrast of two `rgb(r, g, b)` colours as getComputedStyle reports them. */
function contrastRatio(foreground: string, background: string): number {
  const luminance = (color: string) => {
    const [red, green, blue] = color.match(/\d+(\.\d+)?/g)!.slice(0, 3)
      .map(channel => Number(channel) / 255)
      .map(channel => channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
  };
  const [lighter, darker] = [luminance(foreground), luminance(background)].sort((left, right) => right - left);
  return (lighter + 0.05) / (darker + 0.05);
}
