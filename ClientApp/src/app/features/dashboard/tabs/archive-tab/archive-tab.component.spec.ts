import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NEVER, Subject, throwError } from 'rxjs';
import { provideAppLocale } from '../../../../core/utils/locale.utils';
import { ArchiveTabComponent } from './archive-tab.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import { apiResult } from '../../../../core/testing/api-response.testing';
import { createFilterPresetsServiceStub, FilterPresetsServiceStub } from '../../../../core/testing/filter-presets.testing';
import { ArchivedOffer } from '../../../../core/models/archived-offer.model';

const DAY_MS = 24 * 60 * 60 * 1000;
/** `days` days before now, as the API writes dates. */
const daysAgo = (days: number): string => new Date(Date.now() - days * DAY_MS).toISOString();

describe('ArchiveTabComponent', () => {
  let component: ArchiveTabComponent;
  let fixture: ComponentFixture<ArchiveTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;
  let presets: FilterPresetsServiceStub;
  let reload$: Subject<void>;

  // Seen recently for a week; seen two months ago for one day; midday UTC keeps the dates the same in every time zone
  const recent: ArchivedOffer = {
    district: 'Durlach', roomsCount: 3, priceEur: 389000, livingAreaSqm: 82, pricePerMeter: 4744,
    url: 'https://www.immobilienscout24.de/expose/161', source: 'ImmobilienScout24', listingId: '161', hasPreview: true,
    firstSeenUtc: daysAgo(10), lastSeenUtc: daysAgo(3), firstPriceEur: 410000
  };
  const old: ArchivedOffer = {
    district: 'Weststadt', roomsCount: 2, priceEur: 265000, livingAreaSqm: 58, pricePerMeter: 4569,
    url: 'https://www.immowelt.de/expose/7', source: 'Immowelt', listingId: '7', hasPreview: false,
    firstSeenUtc: daysAgo(61), lastSeenUtc: daysAgo(60), firstPriceEur: 265000
  };

  const load = (offers: ArchivedOffer[]) => {
    flatsService.getArchivedOffers.and.returnValue(apiResult({ success: true, data: offers, message: 'Success' }));
    fixture.detectChanges();
  };
  const element = (): HTMLElement => fixture.nativeElement;
  // Without the sort arrows, which only screen readers skip
  const headers = (): string[] =>
    Array.from(element().querySelectorAll('thead th') as NodeListOf<HTMLElement>)
      .map(th => th.textContent!.replace(/[↕↑↓]/g, '').replace(/\s+/g, ' ').trim());
  const rows = (): HTMLTableRowElement[] => Array.from(element().querySelectorAll('tbody tr'));
  const cell = (row: HTMLTableRowElement, selector: string): string => row.querySelector(selector)!.textContent!.trim();
  const chipTexts = (): string[] =>
    Array.from(element().querySelectorAll('.filter-chip') as NodeListOf<HTMLElement>).map(chip => chip.firstChild!.textContent!.trim());
  const lastSeenSelect = (): HTMLSelectElement => element().querySelector('#lastSeenWithin')!;
  const chooseLastSeen = (label: string) => {
    const select = lastSeenSelect();
    select.selectedIndex = Array.from(select.options).findIndex(option => option.textContent!.trim() === label);
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  beforeEach(async () => {
    presets = createFilterPresetsServiceStub();
    reload$ = new Subject<void>();

    await TestBed.configureTestingModule({
      imports: [ArchiveTabComponent],
      providers: [
        provideAppLocale(),
        { provide: FlatsService, useValue: jasmine.createSpyObj('FlatsService', ['getArchivedOffers', 'getCurrentOffers']) },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        { provide: FilterPresetsService, useValue: presets.service },
        { provide: DataReloadService, useValue: { reload$ } },
        { provide: ScrapeStatusService, useValue: jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape'], { status$: NEVER }) }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(ArchiveTabComponent);
    component = fixture.componentInstance;
  });

  describe('Loading', () => {
    it('loads the archived offers, not the current ones', () => {
      load([recent, old]);

      expect(flatsService.getArchivedOffers).toHaveBeenCalledTimes(1);
      expect(flatsService.getCurrentOffers).not.toHaveBeenCalled();
      expect(rows().length).toBe(2);
    });

    it('computes the days each flat was listed', () => {
      load([recent, old]);

      expect(component.currentOffers.map(offer => offer.daysListed)).toEqual([7, 1]);
    });

    it('loads them again on each reload request', () => {
      load([recent]);

      reload$.next();

      expect(flatsService.getArchivedOffers).toHaveBeenCalledTimes(2);
    });

    it('names the archive in a load error', () => {
      flatsService.getArchivedOffers.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      expect(element().querySelector('.alert-danger')!.textContent).toContain('Error loading archived offers: Network error');
    });

    it('renders fifteen skeleton columns while loading', () => {
      flatsService.getArchivedOffers.and.returnValue(NEVER);
      fixture.detectChanges();

      expect(element().querySelectorAll('app-loading-skeleton thead th').length).toBe(15);
    });
  });

  describe('Table', () => {
    it('has First seen, Last seen and Days before Source', () => {
      load([recent]);

      expect(headers().slice(-4)).toEqual(['First seen', 'Last seen', 'Days', 'Source']);
    });

    it('has First price right after Price', () => {
      load([recent]);

      expect(headers().slice(3, 6)).toEqual(['Price (€)', 'First price (€)', 'Living area (m²)']);
    });

    it('shows the first price next to the last one, and - without one', () => {
      load([recent, { ...old, firstPriceEur: undefined }]);

      expect(cell(rows()[0], '.offer-first-price')).toBe('410.000 €');
      expect(cell(rows()[1], '.offer-first-price')).toBe('-');
    });

    it('has one cell per header in every row', () => {
      load([recent, old]);

      for (const row of rows()) {
        expect(row.querySelectorAll('td').length).toBe(headers().length);
      }
    });

    it('shows the dates in German format and the days listed', () => {
      load([{ ...recent, firstSeenUtc: '2026-10-02T12:00:00Z', lastSeenUtc: '2026-10-29T12:00:00Z' }]);
      const [row] = rows();

      expect(cell(row, '.offer-first-seen')).toBe('02.10.2026');
      expect(cell(row, '.offer-last-seen')).toBe('29.10.2026');
      expect(cell(row, '.offer-days-listed')).toBe('27');
    });

    it('shows the archived photo', () => {
      load([recent, old]);

      expect(rows()[0].querySelector('img.offer-thumb')!.getAttribute('src'))
        .toBe('/api/flats/ImmobilienScout24/161/archived-preview');
      expect(rows()[1].querySelector('img')).toBeNull();
    });

    it('says in the links that the listing is probably no longer online', () => {
      load([recent]);

      expect(rows()[0].querySelector('.source-link')!.getAttribute('aria-label'))
        .toBe('Open the offer on ImmobilienScout24 (probably no longer online)');
    });
  });

  describe('Sorting', () => {
    it('sorts by last seen, newest first, until another column is chosen', () => {
      load([old, recent]);

      expect(component.filteredOffers.map(offer => offer.listingId)).toEqual(['161', '7']);
      const lastSeenHeader = Array.from(element().querySelectorAll('thead th')).find(th => th.textContent!.includes('Last seen'))!;
      expect(lastSeenHeader.getAttribute('aria-sort')).toBe('descending');
    });

    it('sorts by the first price through its header', () => {
      load([recent, old]);

      const header = Array.from(element().querySelectorAll('thead th')).find(th => th.textContent!.includes('First price'))!;
      (header.querySelector('button') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.filteredOffers.map(offer => offer.firstPriceEur)).toEqual([265000, 410000]);
    });

    it('sorts by the days listed through its header', () => {
      load([recent, old]);

      const header = Array.from(element().querySelectorAll('thead th')).find(th => th.textContent!.includes('Days'))!;
      (header.querySelector('button') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.filteredOffers.map(offer => offer.daysListed)).toEqual([1, 7]);
    });

    it('offers the archive columns in the cards\' sort control too', () => {
      load([recent]);

      const options = Array.from(element().querySelectorAll('#offerCardSort option')).map(option => option.textContent!.trim());
      expect(options).toContain('Last Seen');
      expect(options.slice(-4)).toEqual(['First Seen', 'Last Seen', 'Days Listed', 'Source']);
      expect(options.slice(2, 4)).toEqual(['Price', 'First Price']);
    });

    it('shows when the flat was seen on the cards', () => {
      load([{ ...recent, firstSeenUtc: '2026-10-02T12:00:00Z', lastSeenUtc: '2026-10-29T12:00:00Z' }]);

      expect(element().querySelector('.offer-card-seen')!.textContent!.replace(/\s+/g, ' ').trim())
        .toBe('Seen: 02.10.2026 – 29.10.2026 (27 days)');
    });
  });

  describe('Last seen filter', () => {
    beforeEach(() => load([recent, old]));

    it('shows all by default', () => {
      expect(lastSeenSelect().selectedOptions[0].textContent!.trim()).toBe('all');
      expect(component.filteredOffers.length).toBe(2);
      expect(chipTexts()).toEqual([]);
    });

    it('keeps the flats last seen within the chosen period, with a chip', () => {
      chooseLastSeen('last 4 weeks');

      expect(component.filteredOffers.map(offer => offer.listingId)).toEqual(['161']);
      expect(chipTexts()).toEqual(['seen in the last 4 weeks']);
    });

    it('is cleared by its chip', () => {
      chooseLastSeen('last 4 weeks');

      (element().querySelector('[aria-label="Remove filter seen in the last 4 weeks"]') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.lastSeen.days).toBeNull();
      expect(component.filteredOffers.length).toBe(2);
    });

    it('is cleared by resetting the filters, with the others', () => {
      chooseLastSeen('last 4 weeks');
      component.priceRangeMax = 300000;
      component.applyFilters();
      fixture.detectChanges();

      (element().querySelector('.filter-reset') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.lastSeen.days).toBeNull();
      expect(component.priceRangeMax).toBeNull();
      expect(component.filteredOffers.length).toBe(2);
      expect(presets.service.selectPreset).toHaveBeenCalledWith(null);
    });

    it('works together with the common filters', () => {
      chooseLastSeen('last year');
      component.selectedDistricts = ['Weststadt'];
      component.applyFilters();

      expect(component.filteredOffers.map(offer => offer.listingId)).toEqual(['7']);
    });
  });

  describe('Empty state', () => {
    it('explains when flats show up here, without a scrape button', () => {
      load([]);

      const emptyState = element().querySelector('app-empty-state')!;
      expect(emptyState.querySelector('h2')!.textContent!.trim()).toBe('No archived offers yet');
      expect(emptyState.querySelector('app-start-scrape-button')).toBeNull();
    });
  });
});
