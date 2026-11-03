import { sourceBadge } from './source.utils';

describe('sourceBadge', () => {
  it('gives each known source a short label and its own colour class', () => {
    expect(sourceBadge('ImmobilienScout24')).toEqual({ label: 'IS24', className: 'source-is24' });
    expect(sourceBadge('Immowelt')).toEqual({ label: 'Immowelt', className: 'source-immowelt' });
    expect(sourceBadge('OhneMakler')).toEqual({ label: 'ohne-makler', className: 'source-ohne-makler' });
  });

  it('keeps the name of an unknown source with a neutral colour', () => {
    expect(sourceBadge('Kleinanzeigen')).toEqual({ label: 'Kleinanzeigen', className: 'source-other' });
  });

  it('shows a dash for a missing source', () => {
    expect(sourceBadge(undefined)).toEqual({ label: '-', className: 'source-other' });
    expect(sourceBadge('')).toEqual({ label: '-', className: 'source-other' });
  });
});
