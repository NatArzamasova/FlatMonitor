import { offerPreviewUrl } from './preview.utils';

describe('offerPreviewUrl', () => {
  it('is the API path of the offer\'s photo', () => {
    expect(offerPreviewUrl({ source: 'ImmobilienScout24', listingId: '161234567', hasPreview: true }))
      .toBe('/api/flats/ImmobilienScout24/161234567/preview');
  });

  it('escapes the path segments', () => {
    expect(offerPreviewUrl({ source: 'A B', listingId: '../x?y', hasPreview: true }))
      .toBe('/api/flats/A%20B/..%2Fx%3Fy/preview');
  });

  it('is null without a preview, a source or a listing id', () => {
    expect(offerPreviewUrl({ source: 'OhneMakler', listingId: '1', hasPreview: false })).toBeNull();
    expect(offerPreviewUrl({ source: 'OhneMakler', listingId: '1' })).toBeNull();
    expect(offerPreviewUrl({ listingId: '1', hasPreview: true })).toBeNull();
    expect(offerPreviewUrl({ source: 'OhneMakler', hasPreview: true })).toBeNull();
  });
});
