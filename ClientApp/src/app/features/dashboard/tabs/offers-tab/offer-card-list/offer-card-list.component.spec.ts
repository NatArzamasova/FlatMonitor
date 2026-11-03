import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideAppLocale } from '../../../../../core/utils/locale.utils';
import { FlatOffer } from '../../../../../core/models/flat-offer.model';
import { TableSort } from '../../../../../core/utils/table-sort';
import { sourceBadge } from '../../../../../core/utils/source.utils';
import { energyClassBadge } from '../../../../../core/utils/energy-class.utils';
import { OfferRow, OfferSortColumn, OFFER_SORT_COLUMNS } from '../offer-row';
import { OfferCardListComponent } from './offer-card-list.component';
import { offerPreviewUrl } from '../../../../../core/utils/preview.utils';

describe('OfferCardListComponent', () => {
  let fixture: ComponentFixture<OfferCardListComponent>;
  let component: OfferCardListComponent;
  let sort: TableSort<FlatOffer, OfferSortColumn>;
  let requested: OfferSortColumn[];

  const row = (offer: FlatOffer, discountPercentage: number | null = null, hasLink = true): OfferRow =>
    ({
      offer, discountPercentage, hasLink, source: sourceBadge(offer.source), energyClass: energyClassBadge(offer.energyEfficiencyClass),
      previewUrl: offerPreviewUrl(offer)
    });

  const fullRow = row({
    district: 'Südstadt',
    roomsCount: 3,
    priceEur: 4200,
    originalPriceEur: 4500,
    livingAreaSqm: 65.5,
    pricePerMeter: 4250,
    url: 'https://example.com/flat1',
    source: 'ImmobilienScout24'
  }, 6.7);

  const element = (): HTMLElement => fixture.nativeElement;
  const cards = (): HTMLElement[] => Array.from(element().querySelectorAll('article.offer-card'));
  // Collapses the template's line breaks and indentation, but keeps the prices' non-breaking space (`\s` would not).
  const text = (card: HTMLElement, selector: string) =>
    card.querySelector(selector)!.textContent!.replace(/[ \t\r\n]+/g, ' ').trim();
  const sortSelect = (): HTMLSelectElement => element().querySelector('#offerCardSort')!;
  const directionButton = (): HTMLButtonElement => element().querySelector('.offer-sort-direction')!;
  const render = (rows: OfferRow[]) => {
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('sort', sort);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OfferCardListComponent],
      providers: [provideAppLocale()]
    }).compileComponents();
    fixture = TestBed.createComponent(OfferCardListComponent);
    component = fixture.componentInstance;
    sort = new TableSort<FlatOffer, OfferSortColumn>();
    requested = [];
    component.sortRequested.subscribe(column => requested.push(column));
  });

  describe('cards', () => {
    it('renders one card per row, in a list', () => {
      render([fullRow, row({ district: 'Mitte', source: 'Immowelt' })]);

      expect(cards().length).toBe(2);
      expect(element().querySelectorAll('ul.offer-cards > li > article').length).toBe(2);
    });

    it('shows district and rooms as the title, the price, the discount, area, €/m² and the source', () => {
      render([fullRow]);
      const card = cards()[0];

      expect(text(card, '.offer-card-title')).toBe('Südstadt · 3 rooms');
      expect(text(card, '.offer-card-price')).toBe('4.200 €');
      expect(text(card, '.price-drop')).toBe('−6,7 %');
      expect(card.querySelector('.price-drop')!.getAttribute('title')).toBe('Was 4.500 € when first listed');
      expect(text(card, '.offer-card-details')).toBe('65,5 m² · 4.250 € / m²');
      expect(text(card, '.source-badge')).toBe('IS24');
      expect(card.querySelector('.source-badge')!.classList).toContain('source-is24');
    });

    it('makes the source pill the link of the card, named after the portal, without an arrow', () => {
      render([fullRow]);
      const link = cards()[0].querySelector('a.source-link') as HTMLAnchorElement;

      expect(link.getAttribute('href')).toBe('https://example.com/flat1');
      expect(link.target).toBe('_blank');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
      expect(link.title).toBe('Open the offer on ImmobilienScout24');
      expect(link.getAttribute('aria-label')).toBe('Open the offer on ImmobilienScout24');
      expect(link.textContent!.trim()).toBe('IS24');
    });

    it('shows the pill without a link when the offer has no usable URL', () => {
      render([row({ district: 'Mitte', source: 'OhneMakler' }, null, false)]);

      expect(cards()[0].querySelector('a')).toBeNull();
      expect(text(cards()[0], '.source-badge')).toBe('ohne-makler');
    });

    it('shows placeholders for missing values and no discount pill without a drop', () => {
      render([row({ district: 'Mitte', roomsCount: 1 })]);
      const card = cards()[0];

      expect(text(card, '.offer-card-title')).toBe('Mitte · 1 room');
      expect(text(card, '.offer-card-price')).toBe('-');
      expect(text(card, '.offer-card-details')).toBe('- m² · - / m²');
      expect(card.querySelector('.price-drop')).toBeNull();
    });

    it('shows the availability when the offer has one', () => {
      render([row({ district: 'Mitte', availability: 'sofort' }), row({ district: 'Weststadt' })]);

      expect(text(cards()[0], '.offer-card-availability')).toBe('Available: sofort');
      expect(cards()[0].querySelector('.offer-card-availability')!.getAttribute('title')).toBe('sofort');
      expect(cards()[1].querySelector('.offer-card-availability')).toBeNull();
    });

    it('shows the monthly Hausgeld when the offer has one, also when it is 0', () => {
      render([
        row({ district: 'Mitte', serviceChargeEur: 310 }),
        row({ district: 'Mitte', serviceChargeEur: 0 }),
        row({ district: 'Weststadt' })
      ]);

      expect(text(cards()[0], '.offer-card-service-charge')).toBe('Hausgeld: 310 € / month');
      expect(text(cards()[1], '.offer-card-service-charge')).toBe('Hausgeld: 0 € / month');
      expect(cards()[2].querySelector('.offer-card-service-charge')).toBeNull();
    });

    it('shows the commission when the offer has one, also when it is 0', () => {
      render([
        row({ district: 'Mitte', commissionPercent: 3.57 }),
        row({ district: 'Mitte', commissionPercent: 0 }),
        row({ district: 'Weststadt' })
      ]);

      expect(text(cards()[0], '.offer-card-commission')).toBe('Provision: 3,57 %');
      expect(text(cards()[1], '.offer-card-commission')).toBe('Provision: 0 %');
      expect(cards()[2].querySelector('.offer-card-commission')).toBeNull();
    });

    it('shows the energy class as a pill in its scale colour, and none without a class', () => {
      render([row({ district: 'Mitte', energyEfficiencyClass: 'A_PLUS' }), row({ district: 'Weststadt' })]);

      const pill = cards()[0].querySelector('.ee-badge') as HTMLElement;
      expect(pill.textContent!.trim()).toBe('A+');
      expect(pill.classList).toContain('ee-a-plus');
      expect(pill.title).toBe('Energy efficiency class A+');
      expect(cards()[1].querySelector('.ee-badge')).toBeNull();
    });

    it('leaves the rooms out of the title when they are unknown', () => {
      render([row({ district: 'Mitte' })]);

      expect(text(cards()[0], '.offer-card-title')).toBe('Mitte');
    });
  });

  describe('sort control', () => {
    it('offers the table columns, with "Default order" while nothing is sorted', () => {
      render([fullRow]);

      const options = Array.from(sortSelect().options).map(option => option.textContent!.trim());
      expect(options).toEqual(['Default order', ...OFFER_SORT_COLUMNS.map(item => item.label)]);
      expect(directionButton().disabled).toBeTrue();
    });

    it('asks for the chosen column, like a click on its header', () => {
      render([fullRow]);

      sortSelect().value = 'priceEur';
      sortSelect().dispatchEvent(new Event('change'));

      expect(requested).toEqual(['priceEur']);
    });

    it('asks for the current column again to toggle the direction', () => {
      sort.toggle('priceEur');
      render([fullRow]);

      expect(sortSelect().value).toBe('priceEur');
      expect(directionButton().getAttribute('aria-label')).toBe('Ascending (switch to descending)');
      directionButton().click();

      expect(requested).toEqual(['priceEur']);
    });

    it('shows the current direction', () => {
      sort.toggle('district');
      sort.toggle('district');
      render([fullRow]);

      expect(directionButton().textContent!.trim()).toBe('↓');
      expect(directionButton().getAttribute('aria-label')).toBe('Descending (switch to ascending)');
      expect(Array.from(sortSelect().options).map(option => option.value)).not.toContain('');
    });

    it('ignores choosing the column that is already sorted', () => {
      sort.toggle('priceEur');
      render([fullRow]);

      component.chooseColumn('priceEur');

      expect(requested).toEqual([]);
    });
  });

  describe('clickable cards', () => {
    let open: jasmine.Spy;

    beforeEach(() => {
      open = spyOn(window, 'open');
      window.getSelection()?.removeAllRanges();
    });

    it('opens the offer on its portal when the card is clicked, but not from its source link', () => {
      render([fullRow]);
      const card = cards()[0];

      card.querySelector<HTMLElement>('.offer-card-title')!.click();
      expect(open).toHaveBeenCalledOnceWith('https://example.com/flat1', '_blank', 'noopener,noreferrer');
      expect(card.classList).toContain('offer-card-link');
      expect(card.title).toBe('Open the offer on ImmobilienScout24');

      const link = card.querySelector('a.source-link') as HTMLAnchorElement;
      link.addEventListener('click', event => event.preventDefault());
      link.click();
      expect(open).toHaveBeenCalledTimes(1);
    });

    it('does not open anything for an offer without a usable URL', () => {
      render([row({ district: 'Mitte', url: 'javascript:alert(1)', source: 'OhneMakler' }, null, false)]);
      const card = cards()[0];

      card.querySelector<HTMLElement>('.offer-card-title')!.click();

      expect(open).not.toHaveBeenCalled();
      expect(card.classList).not.toContain('offer-card-link');
    });
  });

  it('shows the photo left of the text of the card, and a placeholder for an offer without one', () => {
    render([
      row({ district: 'Mitte', roomsCount: 1, source: 'OhneMakler', listingId: '42', hasPreview: true }),
      row({ district: 'Durlach', roomsCount: 2, source: 'OhneMakler', listingId: '43', hasPreview: false })
    ]);

    const [withPhoto, withoutPhoto] = cards();
    const body = withPhoto.querySelector('.card-body')!;
    expect(body.firstElementChild!.tagName.toLowerCase()).toBe('app-offer-preview');
    expect(withPhoto.querySelector('img.offer-thumb')!.getAttribute('src')).toBe('/api/flats/OhneMakler/42/preview');
    expect(withoutPhoto.querySelector('img')).toBeNull();
    expect(withoutPhoto.querySelector('.offer-thumb-empty')).not.toBeNull();
    expect(text(withPhoto, '.offer-card-title')).toBe('Mitte · 1 room');
  });
});
